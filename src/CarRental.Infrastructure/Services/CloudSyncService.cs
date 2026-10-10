using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CarRental.Core.Entities;
using CarRental.Core.Enums;
using CarRental.Core.Interfaces;
using CarRental.Infrastructure.Data;
using CarRental.Infrastructure.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.Services;

/// <summary>
/// خدمة المزامنة السحابية الذكية ثنائية الاتجاه (Two-Way Realtime Sync) مع Supabase وتطبيق الموبايل
/// مصممة للعمل في الخلفية تماماً (Non-Blocking Background Tasks) لضمان بقاء النظام المكتبي سريعاً وأوفلاين 100%
/// </summary>
public static class CloudSyncService
{
    public const string DefaultInternalUrl = "";
    public const string DefaultInternalAnonKey = "";

    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(6) };
    private static readonly string ConfigPath = Path.Combine(AppPaths.BaseDataDirectory, "supabase_cloud_config.json");
    private static bool _isSyncLoopRunning = false;
    public static bool IsSyncPaused { get; set; } = false;

    public record CloudConfig(string Url, string AnonKey, bool AutoSyncEnabled);

    public static CloudConfig GetConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<CloudConfig>(json);
                if (loaded != null && !string.IsNullOrWhiteSpace(loaded.Url) && !string.IsNullOrWhiteSpace(loaded.AnonKey))
                {
                    return loaded;
                }
            }
        }
        catch { }
        return new CloudConfig(DefaultInternalUrl, DefaultInternalAnonKey, true);
    }

    public static void SaveConfig(string url, string anonKey, bool autoSync = true)
    {
        try
        {
            var config = new CloudConfig(url.Trim(), anonKey.Trim(), autoSync);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);

            // حفظ نسخة في مجلد تطبيق الموبايل ليعمل فورياً دون إعادة إدخال
            var webEnvPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "web mange car", ".env"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "web mange car", ".env"),
                @"C:\Users\zaidb.ZAIDDRG\Desktop\car-husen\web mange car\.env"
            };

            var envContent = $"VITE_SUPABASE_URL={url.Trim()}\nVITE_SUPABASE_ANON_KEY={anonKey.Trim()}\n";
            foreach (var p in webEnvPaths)
            {
                try
                {
                    var dir = Path.GetDirectoryName(p);
                    if (dir != null && Directory.Exists(dir))
                    {
                        File.WriteAllText(p, envContent);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    public static bool IsConfigured()
    {
        if (IsSyncPaused) return false;
        var cfg = GetConfig();
        return cfg != null && cfg.AutoSyncEnabled && !string.IsNullOrWhiteSpace(cfg.Url) && !string.IsNullOrWhiteSpace(cfg.AnonKey);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, object? body = null)
    {
        var cfg = GetConfig();
        if (cfg == null) throw new InvalidOperationException("Supabase is not configured.");

        var fullUrl = cfg.Url.TrimEnd('/') + "/rest/v1/" + endpoint.TrimStart('/');
        var request = new HttpRequestMessage(method, fullUrl);
        request.Headers.Add("apikey", cfg.AnonKey);
        request.Headers.Add("Authorization", $"Bearer {cfg.AnonKey}");

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    // =========================================================================
    // 1. مزامنة المستخدمين وتغيير كلمة السر (PIN) لحظياً
    // =========================================================================
    public static void TriggerSyncUser(User user)
    {
        if (!IsConfigured()) return;
        _ = Task.Run(async () =>
        {
            try
            {
                var payload = new
                {
                    username = user.Username,
                    full_name = user.FullName,
                    pin_code = user.PasswordHash,
                    role = user.Role.ToString(),
                    phone_number = user.PhoneNumber,
                    is_active = user.IsActive
                };

                // استخدام Upsert مباشر وذري على Supabase عبر PostgREST merge-duplicates
                using var req = CreateRequest(HttpMethod.Post, "users?on_conflict=username", payload);
                req.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
                var resp = await _httpClient.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    // محاولة احتياطية عبر PATCH
                    using var reqPatch = CreateRequest(HttpMethod.Patch, $"users?username=eq.{Uri.EscapeDataString(user.Username)}", payload);
                    await _httpClient.SendAsync(reqPatch);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudSync] TriggerSyncUser error: {ex.Message}");
            }
        });
    }

    public static void TriggerSyncDeleteUser(string username)
    {
        if (!IsConfigured() || string.IsNullOrWhiteSpace(username)) return;
        _ = Task.Run(async () =>
        {
            try
            {
                using var req = CreateRequest(HttpMethod.Delete, $"users?username=eq.{Uri.EscapeDataString(username)}");
                await _httpClient.SendAsync(req);
            }
            catch { }
        });
    }

    // =========================================================================
    // 2. مزامنة السيارات وتعديل الأسماء والعدادات والحالات والصور لحظياً
    // =========================================================================
    public static string? NormalizeCarImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;
        var trimmed = imageUrl.Trim();
        if (trimmed.StartsWith("data:image", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }
        if (trimmed.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
        {
            return "/" + trimmed;
        }
        return trimmed;
    }

    public static void TriggerSyncVehicle(Vehicle vehicle, string? oldPlateNumber = null)
    {
        if (!IsConfigured()) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await SyncVehicleDirectAsync(vehicle, oldPlateNumber);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudSync] TriggerSyncVehicle error: {ex.Message}");
            }
        });
    }

    public static async Task SyncVehicleDirectAsync(Vehicle vehicle, string? oldPlateNumber = null)
    {
        if (!IsConfigured()) return;
        try
        {
            var normalizedImageUrl = NormalizeCarImageUrl(vehicle.ImageUrl);

            var payload = new
            {
                plate_number = vehicle.PlateNumber.Trim(),
                brand = vehicle.Brand.Trim(),
                model = vehicle.Model.Trim(),
                model_year = vehicle.ModelYear,
                color = vehicle.Color.Trim(),
                vin_number = string.IsNullOrWhiteSpace(vehicle.VinNumber) ? null : vehicle.VinNumber.Trim(),
                current_odometer = vehicle.CurrentOdometer,
                daily_standard_rate = vehicle.DailyStandardRate,
                status = vehicle.Status.ToString(),
                license_expiry_date = vehicle.LicenseExpiryDate.ToString("yyyy-MM-dd"),
                insurance_expiry_date = vehicle.InsuranceExpiryDate.ToString("yyyy-MM-dd"),
                insurance_company = string.IsNullOrWhiteSpace(vehicle.InsuranceCompany) ? null : vehicle.InsuranceCompany.Trim(),
                next_oil_change_odometer = vehicle.NextOilChangeOdometer > 0 ? (int?)vehicle.NextOilChangeOdometer : null,
                next_maintenance_date = vehicle.NextMaintenanceDate > DateTime.MinValue ? vehicle.NextMaintenanceDate.ToString("yyyy-MM-dd") : null,
                is_maintenance_critical = vehicle.IsMaintenanceCritical,
                critical_maintenance_reason = vehicle.CriticalMaintenanceReason,
                image_url = normalizedImageUrl,
                is_active = vehicle.IsActive
            };

            // إذا تم تغيير رقم اللوحة، نقوم بتحديث السجل السحابي القديم أولاً بالرقم والبيانات الجديدة
            if (!string.IsNullOrWhiteSpace(oldPlateNumber) && !oldPlateNumber.Trim().Equals(vehicle.PlateNumber.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                using var reqOldPatch = CreateRequest(HttpMethod.Patch, $"vehicles?plate_number=eq.{Uri.EscapeDataString(oldPlateNumber.Trim())}", payload);
                reqOldPatch.Headers.Add("Prefer", "return=representation");
                var respOld = await _httpClient.SendAsync(reqOldPatch);
                if (respOld.IsSuccessStatusCode)
                {
                    var oldBody = await respOld.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(oldBody) && oldBody.Trim() != "[]")
                    {
                        return; // تم التحديث بنجاح تام على السجل القديم
                    }
                }
            }

            // تحديث مباشر بالـ PATCH مع طلب إرجاع التمثيل
            using var reqPatch = CreateRequest(HttpMethod.Patch, $"vehicles?plate_number=eq.{Uri.EscapeDataString(vehicle.PlateNumber.Trim())}", payload);
            reqPatch.Headers.Add("Prefer", "return=representation");
            var resp = await _httpClient.SendAsync(reqPatch);
            var body = await resp.Content.ReadAsStringAsync();

            // إذا كانت السيارة جديدة غير موجودة مسبقاً في السحابة
            if (!resp.IsSuccessStatusCode || body.Trim() == "[]" || string.IsNullOrWhiteSpace(body))
            {
                using var reqPost = CreateRequest(HttpMethod.Post, "vehicles", payload);
                reqPost.Headers.Add("Prefer", "return=representation");
                await _httpClient.SendAsync(reqPost);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CloudSync] SyncVehicleDirectAsync error: {ex.Message}");
        }
    }

    public static void TriggerSyncDeleteVehicle(string plateNumber)
    {
        if (!IsConfigured() || string.IsNullOrWhiteSpace(plateNumber)) return;
        _ = Task.Run(async () =>
        {
            try
            {
                using var req = CreateRequest(HttpMethod.Delete, $"vehicles?plate_number=eq.{Uri.EscapeDataString(plateNumber.Trim())}");
                await _httpClient.SendAsync(req);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudSync] TriggerSyncDeleteVehicle error: {ex.Message}");
            }
        });
    }

    // =========================================================================
    // 3. مزامنة العملاء والديون والأسماء
    // =========================================================================
    public static void TriggerSyncCustomer(Customer customer)
    {
        if (!IsConfigured()) return;
        _ = Task.Run(async () =>
        {
            try
            {
                var payload = new
                {
                    national_id = customer.NationalIdOrPassport,
                    full_name = customer.FullName,
                    primary_phone = customer.PrimaryPhone,
                    emergency_phone = customer.EmergencyPhone,
                    license_number = customer.LicenseNumber,
                    license_expiry_date = customer.LicenseExpiryDate.ToString("yyyy-MM-dd"),
                    date_of_birth = customer.DateOfBirth.ToString("yyyy-MM-dd"),
                    address = customer.Address ?? "طوباس",
                    total_debts = customer.TotalDebts,
                    notes = customer.Notes
                };

                using var reqPatch = CreateRequest(HttpMethod.Patch, $"customers?national_id=eq.{Uri.EscapeDataString(customer.NationalIdOrPassport)}", payload);
                reqPatch.Headers.Add("Prefer", "return=representation");
                var resp = await _httpClient.SendAsync(reqPatch);
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode || body.Trim() == "[]" || string.IsNullOrWhiteSpace(body))
                {
                    using var reqPost = CreateRequest(HttpMethod.Post, "customers", payload);
                    await _httpClient.SendAsync(reqPost);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudSync] TriggerSyncCustomer error: {ex.Message}");
            }
        });
    }

    public static void TriggerSyncDeleteCustomer(string nationalId)
    {
        if (!IsConfigured() || string.IsNullOrWhiteSpace(nationalId)) return;
        _ = Task.Run(async () =>
        {
            try
            {
                // 1. إزالة أي سجل حظر في القائمة السوداء
                using var reqBlacklist = CreateRequest(HttpMethod.Delete, $"blacklist_records?national_id=eq.{Uri.EscapeDataString(nationalId.Trim())}");
                await _httpClient.SendAsync(reqBlacklist);

                // 2. إزالة سجل العميل من جدول customers
                using var reqCustomer = CreateRequest(HttpMethod.Delete, $"customers?national_id=eq.{Uri.EscapeDataString(nationalId.Trim())}");
                await _httpClient.SendAsync(reqCustomer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudSync] TriggerSyncDeleteCustomer error: {ex.Message}");
            }
        });
    }

    // =========================================================================
    // 4. مزامنة إنشاء عقد تأجير من السيستم المكتبي -> تحديث سوبابيز والموبايل فورياً
    // =========================================================================
    public static void TriggerSyncRentalCreated(Contract contract, Vehicle vehicle, Customer customer)
    {
        if (!IsConfigured()) return;
        _ = Task.Run(async () =>
        {
            try
            {
                string? cloudVehicleId = null;
                string? cloudCustomerId = null;

                // تحديث المركبة والبحث عن العميل بالتوازي (Parallel Requests)
                var updateVehicleTask = Task.Run(async () =>
                {
                    try
                    {
                        using var reqVehicle = CreateRequest(HttpMethod.Patch, $"vehicles?plate_number=eq.{Uri.EscapeDataString(vehicle.PlateNumber)}", new
                        {
                            status = "Rented",
                            current_odometer = vehicle.CurrentOdometer
                        });
                        reqVehicle.Headers.Add("Prefer", "return=representation");
                        var respVehicle = await _httpClient.SendAsync(reqVehicle);
                        if (respVehicle.IsSuccessStatusCode)
                        {
                            var vJson = await respVehicle.Content.ReadAsStringAsync();
                            using var vDoc = JsonDocument.Parse(vJson);
                            if (vDoc.RootElement.GetArrayLength() > 0)
                            {
                                cloudVehicleId = vDoc.RootElement[0].GetProperty("id").GetString();
                            }
                        }
                    }
                    catch { }
                });

                var lookupCustomerTask = Task.Run(async () =>
                {
                    try
                    {
                        using var reqC = CreateRequest(HttpMethod.Get, $"customers?national_id=eq.{Uri.EscapeDataString(customer.NationalIdOrPassport)}&select=id");
                        var respC = await _httpClient.SendAsync(reqC);
                        if (respC.IsSuccessStatusCode)
                        {
                            var json = await respC.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.GetArrayLength() > 0)
                            {
                                cloudCustomerId = doc.RootElement[0].GetProperty("id").GetString();
                            }
                        }

                        if (cloudCustomerId == null)
                        {
                            var custPayload = new
                            {
                                national_id = customer.NationalIdOrPassport,
                                full_name = customer.FullName,
                                primary_phone = customer.PrimaryPhone,
                                license_number = customer.LicenseNumber,
                                license_expiry_date = customer.LicenseExpiryDate.ToString("yyyy-MM-dd"),
                                date_of_birth = customer.DateOfBirth.ToString("yyyy-MM-dd"),
                                address = customer.Address ?? "طوباس",
                                total_debts = customer.TotalDebts
                            };
                            using var reqNewC = CreateRequest(HttpMethod.Post, "customers", custPayload);
                            reqNewC.Headers.Add("Prefer", "return=representation");
                            var respNewC = await _httpClient.SendAsync(reqNewC);
                            if (respNewC.IsSuccessStatusCode)
                            {
                                var cJson = await respNewC.Content.ReadAsStringAsync();
                                using var cDoc = JsonDocument.Parse(cJson);
                                if (cDoc.RootElement.GetArrayLength() > 0)
                                {
                                    cloudCustomerId = cDoc.RootElement[0].GetProperty("id").GetString();
                                }
                            }
                        }
                    }
                    catch { }
                });

                await Task.WhenAll(updateVehicleTask, lookupCustomerTask);

                // إدراج العقد في جدول contracts في سوبابيز
                if (cloudVehicleId != null && cloudCustomerId != null)
                {
                    var contractPayload = new
                    {
                        contract_number = contract.ContractNumber,
                        customer_id = cloudCustomerId,
                        vehicle_id = cloudVehicleId,
                        start_date_time = contract.StartDateTime.ToString("o"),
                        expected_return_date_time = contract.ExpectedReturnDateTime.ToString("o"),
                        is_open_ended = contract.IsOpenEnded,
                        daily_agreed_rate = contract.DailyAgreedRate,
                        total_agreed_amount = contract.TotalAgreedAmount,
                        paid_amount = contract.PaidAmount,
                        remaining_amount = contract.RemainingAmount,
                        payment_mode = contract.PaymentMode.ToString(),
                        status = "Active",
                        promissory_note_amount = contract.PromissoryNoteAmount,
                        start_odometer = contract.StartOdometer,
                        created_by_staff = contract.CreatedByStaffName,
                        notes = contract.Notes ?? "تأجير عبر سيستم المكتب"
                    };

                    using var reqContract = CreateRequest(HttpMethod.Post, "contracts", contractPayload);
                    await _httpClient.SendAsync(reqContract);
                }

                // تحديث ذمم العميل في سوبابيز
                if (contract.RemainingAmount > 0)
                {
                    using var reqDebt = CreateRequest(HttpMethod.Patch, $"customers?national_id=eq.{Uri.EscapeDataString(customer.NationalIdOrPassport)}", new
                    {
                        total_debts = customer.TotalDebts
                    });
                    await _httpClient.SendAsync(reqDebt);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudSync] TriggerSyncRentalCreated failed: {ex.Message}");
            }
        });
    }

    // =========================================================================
    // 5. مزامنة إرجاع / تسوية أو إلغاء العقد -> إعادة السيارة لمتاحة بالسحابة فوراً
    // =========================================================================
    public static void TriggerSyncRentalClosed(string? plateNumber, string? contractNumber)
    {
        if (!IsConfigured()) return;
        _ = Task.Run(async () =>
        {
            try
            {
                var tasks = new List<Task>();

                // أ. إعادة المركبة فوراً إلى حالة Available في سوبابيز بالتوازي
                if (!string.IsNullOrWhiteSpace(plateNumber))
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        using var reqV = CreateRequest(HttpMethod.Patch, $"vehicles?plate_number=eq.{Uri.EscapeDataString(plateNumber)}", new
                        {
                            status = "Available"
                        });
                        await _httpClient.SendAsync(reqV);
                    }));
                }

                // ب. تحديث حالة العقد إلى Completed بالتوازي
                if (!string.IsNullOrWhiteSpace(contractNumber))
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        using var reqC = CreateRequest(HttpMethod.Patch, $"contracts?contract_number=eq.{Uri.EscapeDataString(contractNumber)}", new
                        {
                            status = "Completed"
                        });
                        await _httpClient.SendAsync(reqC);
                    }));
                }

                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudSync] TriggerSyncRentalClosed failed: {ex.Message}");
            }
        });
    }

    // =========================================================================
    // 5.1. مزامنة تمديد أو تعديل عقد قائم فورياً
    // =========================================================================
    public static void TriggerSyncContractUpdated(Contract contract)
    {
        if (!IsConfigured()) return;
        _ = Task.Run(async () =>
        {
            try
            {
                var payload = new
                {
                    expected_return_date_time = contract.ExpectedReturnDateTime.ToString("o"),
                    daily_agreed_rate = contract.DailyAgreedRate,
                    total_agreed_amount = contract.TotalAgreedAmount,
                    paid_amount = contract.PaidAmount,
                    remaining_amount = contract.RemainingAmount,
                    notes = contract.Notes
                };

                using var req = CreateRequest(HttpMethod.Patch, $"contracts?contract_number=eq.{Uri.EscapeDataString(contract.ContractNumber)}", payload);
                await _httpClient.SendAsync(req);

                if (contract.Customer != null)
                {
                    using var reqCust = CreateRequest(HttpMethod.Patch, $"customers?national_id=eq.{Uri.EscapeDataString(contract.Customer.NationalIdOrPassport)}", new
                    {
                        total_debts = contract.Customer.TotalDebts
                    });
                    await _httpClient.SendAsync(reqCust);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudSync] TriggerSyncContractUpdated error: {ex.Message}");
            }
        });
    }

    // =========================================================================
    // 5.2. مزامنة ديون وذمم العميل فورياً
    // =========================================================================
    public static void TriggerSyncCustomerDebt(string nationalId, decimal debts)
    {
        if (!IsConfigured() || string.IsNullOrWhiteSpace(nationalId)) return;
        _ = Task.Run(async () =>
        {
            try
            {
                using var reqCust = CreateRequest(HttpMethod.Patch, $"customers?national_id=eq.{Uri.EscapeDataString(nationalId)}", new
                {
                    total_debts = debts
                });
                await _httpClient.SendAsync(reqCust);
            }
            catch { }
        });
    }

    // =========================================================================
    // 6. مزامنة ريست وتصفير النظام
    // =========================================================================
    public static void TriggerSyncReset()
    {
        if (!IsConfigured()) return;
        _ = Task.Run(async () =>
        {
            try
            {
                var tasks = new List<Task>();

                using var reqTrans = CreateRequest(HttpMethod.Delete, "financial_transactions?id=neq.00000000-0000-0000-0000-000000000000");
                tasks.Add(_httpClient.SendAsync(reqTrans));

                using var reqNotes = CreateRequest(HttpMethod.Delete, "promissory_notes?id=neq.00000000-0000-0000-0000-000000000000");
                tasks.Add(_httpClient.SendAsync(reqNotes));

                using var reqContracts = CreateRequest(HttpMethod.Delete, "contracts?id=neq.00000000-0000-0000-0000-000000000000");
                tasks.Add(_httpClient.SendAsync(reqContracts));

                using var reqVehicles = CreateRequest(HttpMethod.Patch, "vehicles?id=neq.00000000-0000-0000-0000-000000000000", new 
                { 
                    status = "Available",
                    is_maintenance_critical = false,
                    critical_maintenance_reason = (string?)null
                });
                tasks.Add(_httpClient.SendAsync(reqVehicles));

                using var reqCustomers = CreateRequest(HttpMethod.Patch, "customers?id=neq.00000000-0000-0000-0000-000000000000", new { total_debts = 0 });
                tasks.Add(_httpClient.SendAsync(reqCustomers));

                await Task.WhenAll(tasks);
                Console.WriteLine("[CloudSync] Successfully synced operational reset to Supabase!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CloudSync] TriggerSyncReset error: {ex.Message}");
            }
        });
    }

    public static async Task PushUsersToCloudAsync(IDbContextFactory<CarRentalDbContext> dbFactory)
    {
        if (!IsConfigured()) return;
        try
        {
            using var context = dbFactory.CreateDbContext();
            var users = await context.Users.AsNoTracking().ToListAsync();
            foreach (var user in users)
            {
                TriggerSyncUser(user);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CloudSync] PushUsersToCloudAsync error: {ex.Message}");
        }
    }

    // =========================================================================
    // 7. المحرك التلقائي فائق السرعة ثنائي الاتجاه (Ultra-Fast 1.2s Realtime Background Sync)
    // =========================================================================
    public static void StartTwoWaySyncBackground(IDbContextFactory<CarRentalDbContext> dbFactory, IRentalSystemService rentalService)
    {
        if (_isSyncLoopRunning) return;
        _isSyncLoopRunning = true;

        Task.Run(async () =>
        {
            // تطهير سحابي استباقي لمنع أي مركبات وهمية (CONC-) أو سجلات أيتام
            try
            {
                using var initialCtx = dbFactory.CreateDbContext();
                await PurgeOrphanCloudVehiclesAndCustomersAsync(initialCtx);
            }
            catch { }

            // مزامنة فورية لكافة المستخدمين وكلمات سرهم (PINs) إلى السحابة عند إقلاع البرنامج
            await PushUsersToCloudAsync(dbFactory);

            long syncCycle = 0;
            while (true)
            {
                try
                {
                    // تأخير ذكي متوازن (3 ثواني) لضمان الاستجابة السريعة مع حماية حصة الخطة المجانية 100%
                    await Task.Delay(TimeSpan.FromMilliseconds(3000));
                    syncCycle++;

                    if (!IsConfigured()) continue;

                    bool isReconcileCycle = (syncCycle % 15 == 0); // كل 45 ثانية فحص عميق ومطابقة للأسماء والصور
                    bool shouldCheckCustomers = (syncCycle % 10 == 0); // كل 30 ثانية فحص ديون العملاء

                    // استدعاء حقول خفيفة للغاية لتوفير استهلاك الباندويث والبقاء تماماً ضمن حد الـ 2GB شهرياً
                    string vehicleQuery = isReconcileCycle 
                        ? "vehicles?select=plate_number,status,current_odometer,brand,model,image_url" 
                        : "vehicles?select=plate_number,status,current_odometer";

                    using var reqVehicles = CreateRequest(HttpMethod.Get, vehicleQuery);
                    using var reqContracts = CreateRequest(HttpMethod.Get, "contracts?status=eq.Active&select=*,vehicles(plate_number),customers(national_id,full_name,primary_phone)");

                    var taskV = _httpClient.SendAsync(reqVehicles);
                    var taskCnt = _httpClient.SendAsync(reqContracts);
                    Task<HttpResponseMessage?> taskC = shouldCheckCustomers 
                        ? Task.Run<HttpResponseMessage?>(async () =>
                          {
                              using var req = CreateRequest(HttpMethod.Get, "customers?select=national_id,full_name,primary_phone,license_number,license_expiry_date,date_of_birth,address,total_debts");
                              return await _httpClient.SendAsync(req);
                          })
                        : Task.FromResult<HttpResponseMessage?>(null);

                    await Task.WhenAll(taskV, taskCnt, taskC);

                    var respV = await taskV;
                    var respCnt = await taskCnt;
                    var respC = await taskC;

                    // 1. فحص وتحديث حالات وسجلات السيارات في السيستم المكتبي والسحابة تلقائياً دون أي كبسة مزامنة
                    if (respV.IsSuccessStatusCode)
                    {
                        var json = await respV.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var cloudVehicles = doc.RootElement;

                        using var context = dbFactory.CreateDbContext();
                        var localVehicles = await context.Vehicles.ToListAsync();
                        bool stateChanged = false;

                        var cloudPlateMap = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

                        if (cloudVehicles.GetArrayLength() > 0)
                        {
                            foreach (var cv in cloudVehicles.EnumerateArray())
                            {
                                if (cv.TryGetProperty("plate_number", out var pVal) && pVal.GetString() is string plate && !string.IsNullOrWhiteSpace(plate))
                                {
                                    cloudPlateMap[plate.Trim()] = cv;
                                    var cStatusStr = cv.TryGetProperty("status", out var sVal) ? sVal.GetString() : null;

                                    var lv = localVehicles.FirstOrDefault(v => v.PlateNumber.Equals(plate, StringComparison.OrdinalIgnoreCase));
                                    if (lv != null && !string.IsNullOrEmpty(cStatusStr))
                                    {
                                        if (Enum.TryParse<VehicleStatus>(cStatusStr, out var cStatus))
                                        {
                                            if (lv.Status != cStatus)
                                            {
                                                lv.Status = cStatus;
                                                stateChanged = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // فحص عميق للمطابقة كل 15 دورة (حوالي كل 45 ثانية) لسحب/رفع أي فروقات بالصور أو الأسماء
                        if (isReconcileCycle)
                        {
                            // أ. فحص المركبات المحلية للتأكد من وجودها وصحة صورتها وبياناتها في السحابة
                            foreach (var lv in localVehicles)
                            {
                                var cleanPlate = lv.PlateNumber.Trim();
                                var normImage = NormalizeCarImageUrl(lv.ImageUrl);

                                if (!cloudPlateMap.TryGetValue(cleanPlate, out var cv))
                                {
                                    // المركبة غير موجودة في السحابة -> رفعها فورياً
                                    _ = SyncVehicleDirectAsync(lv);
                                }
                                else
                                {
                                    var cloudImg = cv.TryGetProperty("image_url", out var imgVal) ? imgVal.GetString() : null;
                                    var cloudBrand = cv.TryGetProperty("brand", out var bVal) ? bVal.GetString() : null;
                                    var cloudModel = cv.TryGetProperty("model", out var mVal) ? mVal.GetString() : null;

                                    // إذا كانت الصورة أو الاسم في السحابة غير محدث أو فارغ
                                    if ((normImage != null && !string.Equals(cloudImg, normImage, StringComparison.OrdinalIgnoreCase)) ||
                                        (normImage == null && cloudImg != null) ||
                                        !string.Equals(cloudBrand, lv.Brand.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                        !string.Equals(cloudModel, lv.Model.Trim(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        _ = SyncVehicleDirectAsync(lv);
                                    }
                                }
                            }

                            // ب. حذف أي مركبة وهمية أو قديمة في السحابة لم تعد موجودة محلياً
                            var localPlateSet = new HashSet<string>(localVehicles.Select(v => v.PlateNumber.Trim()), StringComparer.OrdinalIgnoreCase);
                            foreach (var kvp in cloudPlateMap)
                            {
                                var cloudPlate = kvp.Key;
                                if (!localPlateSet.Contains(cloudPlate) &&
                                    !cloudPlate.StartsWith("CONC-", StringComparison.OrdinalIgnoreCase) &&
                                    !cloudPlate.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase))
                                {
                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            using var delReq = CreateRequest(HttpMethod.Delete, $"vehicles?plate_number=eq.{Uri.EscapeDataString(cloudPlate)}");
                                            await _httpClient.SendAsync(delReq);
                                        }
                                        catch { }
                                    });
                                }
                            }
                        }

                        if (stateChanged)
                        {
                            await context.SaveChangesAsync();
                            rentalService.NotifyContractsDataChanged();
                        }
                    }

                    // 2. فحص العقود النشطة في سوبابيز وسحبها للنظام المكتبي فورياً
                    if (respCnt.IsSuccessStatusCode)
                    {
                        var cntJson = await respCnt.Content.ReadAsStringAsync();
                        using var cntDoc = JsonDocument.Parse(cntJson);
                        var cloudContracts = cntDoc.RootElement;

                        using var context = dbFactory.CreateDbContext();
                        var localContracts = await context.Contracts
                            .Include(c => c.Vehicle)
                            .Include(c => c.Customer)
                            .Where(c => c.Status == ContractStatus.Active)
                            .ToListAsync();

                        var localVehiclesList = await context.Vehicles.ToListAsync();
                        var localCustomersList = await context.Customers.ToListAsync();

                        var activeCloudContractNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        bool contractStateChanged = false;

                        if (cloudContracts.GetArrayLength() > 0)
                        {
                            foreach (var cc in cloudContracts.EnumerateArray())
                            {
                                var cNo = cc.GetProperty("contract_number").GetString();
                                if (string.IsNullOrEmpty(cNo)) continue;
                                activeCloudContractNumbers.Add(cNo);

                                if (!localContracts.Any(c => c.ContractNumber.Equals(cNo, StringComparison.OrdinalIgnoreCase)))
                                {
                                    string? plate = null;
                                    if (cc.TryGetProperty("vehicles", out var vProp) && vProp.ValueKind == JsonValueKind.Object && vProp.TryGetProperty("plate_number", out var pVal))
                                    {
                                        plate = pVal.GetString();
                                    }

                                    string? nationalId = null;
                                    string? custName = null;
                                    string? custPhone = null;
                                    if (cc.TryGetProperty("customers", out var cProp) && cProp.ValueKind == JsonValueKind.Object)
                                    {
                                        if (cProp.TryGetProperty("national_id", out var nidVal)) nationalId = nidVal.GetString();
                                        if (cProp.TryGetProperty("full_name", out var fnVal)) custName = fnVal.GetString();
                                        if (cProp.TryGetProperty("primary_phone", out var phVal)) custPhone = phVal.GetString();
                                    }

                                    if (!string.IsNullOrEmpty(plate))
                                    {
                                        var targetVehicle = localVehiclesList.FirstOrDefault(v => v.PlateNumber.Trim().Equals(plate.Trim(), StringComparison.OrdinalIgnoreCase));
                                        if (targetVehicle != null)
                                        {
                                            Customer? targetCustomer = null;
                                            if (!string.IsNullOrEmpty(nationalId))
                                            {
                                                targetCustomer = localCustomersList.FirstOrDefault(c => c.NationalIdOrPassport.Trim().Equals(nationalId.Trim(), StringComparison.OrdinalIgnoreCase));
                                            }

                                            if (targetCustomer == null)
                                            {
                                                targetCustomer = new Customer
                                                {
                                                    Id = Guid.NewGuid(),
                                                    FullName = custName ?? "مستأجر عبر الموبايل",
                                                    NationalIdOrPassport = nationalId ?? $"ID-{DateTime.Now.Ticks % 100000000}",
                                                    PrimaryPhone = custPhone ?? "0599000000",
                                                    LicenseNumber = nationalId ?? "DL-ONLINE",
                                                    LicenseExpiryDate = DateTime.Today.AddYears(2),
                                                    DateOfBirth = new DateTime(1995, 1, 1),
                                                    Address = "طوباس"
                                                };
                                                await context.Customers.AddAsync(targetCustomer);
                                                localCustomersList.Add(targetCustomer);
                                            }

                                            DateTime startDt = DateTime.Now;
                                            if (cc.TryGetProperty("start_date_time", out var sdt) && sdt.GetString() is string sdtStr && DateTime.TryParse(sdtStr, out var parsedStart))
                                            {
                                                startDt = parsedStart;
                                            }

                                            DateTime endDt = DateTime.Now.AddDays(1);
                                            if (cc.TryGetProperty("expected_return_date_time", out var edt) && edt.GetString() is string edtStr && DateTime.TryParse(edtStr, out var parsedEnd))
                                            {
                                                endDt = parsedEnd;
                                            }

                                            decimal dailyRate = cc.TryGetProperty("daily_agreed_rate", out var drVal) && drVal.TryGetDecimal(out var dr) ? dr : targetVehicle.DailyStandardRate;
                                            decimal totalAmount = cc.TryGetProperty("total_agreed_amount", out var taVal) && taVal.TryGetDecimal(out var ta) ? ta : dailyRate;
                                            decimal paidAmount = cc.TryGetProperty("paid_amount", out var paVal) && paVal.TryGetDecimal(out var pa) ? pa : 0;
                                            decimal remAmount = cc.TryGetProperty("remaining_amount", out var raVal) && raVal.TryGetDecimal(out var ra) ? ra : (totalAmount - paidAmount);
                                            decimal promAmount = cc.TryGetProperty("promissory_note_amount", out var pnaVal) && pnaVal.TryGetDecimal(out var pna) ? pna : 10000;
                                            string? notes = cc.TryGetProperty("notes", out var nVal) ? nVal.GetString() : "تأجير عبر تطبيق الموبايل";
                                            string? promNo = cc.TryGetProperty("promissory_note_number", out var pnnVal) ? pnnVal.GetString() : null;

                                            var newLocalContract = new Contract
                                            {
                                                Id = Guid.NewGuid(),
                                                ContractNumber = cNo,
                                                VehicleId = targetVehicle.Id,
                                                CustomerId = targetCustomer.Id,
                                                StartDateTime = startDt,
                                                ExpectedReturnDateTime = endDt,
                                                IsOpenEnded = false,
                                                DailyAgreedRate = dailyRate,
                                                TotalAgreedAmount = totalAmount,
                                                PaidAmount = paidAmount,
                                                RemainingAmount = remAmount,
                                                PaymentMode = PaymentMode.DeferredOnReturn,
                                                Status = ContractStatus.Active,
                                                PromissoryNoteAmount = promAmount,
                                                PromissoryNoteNumber = promNo ?? $"PN-M{DateTime.Now.Year}-{new Random().Next(1000, 9999)}",
                                                StartOdometer = targetVehicle.CurrentOdometer,
                                                CreatedByStaffName = cc.TryGetProperty("created_by_staff", out var cbsVal) && !string.IsNullOrWhiteSpace(cbsVal.GetString()) ? cbsVal.GetString()! : "تطبيق الهاتف",
                                                Notes = notes,
                                                CreatedAt = DateTime.UtcNow
                                            };

                                            var newPromNote = new PromissoryNote
                                            {
                                                Id = Guid.NewGuid(),
                                                NoteNumber = newLocalContract.PromissoryNoteNumber,
                                                ContractId = newLocalContract.Id,
                                                CustomerId = targetCustomer.Id,
                                                Amount = promAmount,
                                                IssueDate = startDt.Date,
                                                DueDate = endDt.Date,
                                                Status = PromissoryNoteStatus.HeldInTrust,
                                                LegalText = $"كمبيالة ضمان للمركبة {targetVehicle.Brand} {targetVehicle.Model} لوحة {targetVehicle.PlateNumber}"
                                            };

                                            targetVehicle.Status = VehicleStatus.Rented;
                                            if (remAmount > 0)
                                            {
                                                targetCustomer.TotalDebts += remAmount;
                                            }

                                            await context.Contracts.AddAsync(newLocalContract);
                                            await context.PromissoryNotes.AddAsync(newPromNote);
                                            contractStateChanged = true;
                                        }
                                    }
                                }
                            }
                        }

                        // فحص العقود التي تم إنهاؤها أو تسليمها في تطبيق الموبايل
                        foreach (var localC in localContracts.Where(c => c.ContractNumber.StartsWith("CRM-", StringComparison.OrdinalIgnoreCase)))
                        {
                            if (!activeCloudContractNumbers.Contains(localC.ContractNumber))
                            {
                                localC.Status = ContractStatus.Closed;
                                localC.ActualReturnDateTime = DateTime.Now;
                                if (localC.Vehicle != null)
                                {
                                    localC.Vehicle.Status = VehicleStatus.Available;
                                }
                                contractStateChanged = true;
                            }
                        }

                        if (contractStateChanged)
                        {
                            await context.SaveChangesAsync();
                            rentalService.NotifyContractsDataChanged();
                        }
                    }

                    // 3. فحص وتحديث ديون وتفاصيل العملاء من السحابة دورياً
                    if (respC != null && respC.IsSuccessStatusCode)
                    {
                        var json = await respC.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var cloudCustomers = doc.RootElement;

                        if (cloudCustomers.GetArrayLength() > 0)
                        {
                            using var context = dbFactory.CreateDbContext();
                            var localCustomersList = await context.Customers.ToListAsync();
                            bool custChanged = false;

                            foreach (var cc in cloudCustomers.EnumerateArray())
                            {
                                var natId = cc.GetProperty("national_id").GetString();
                                if (string.IsNullOrEmpty(natId)) continue;

                                decimal cloudDebts = 0;
                                if (cc.TryGetProperty("total_debts", out var tdVal) && tdVal.TryGetDecimal(out var cd))
                                {
                                    cloudDebts = cd;
                                }

                                var existing = localCustomersList.FirstOrDefault(c => c.NationalIdOrPassport.Trim().Equals(natId.Trim(), StringComparison.OrdinalIgnoreCase));
                                if (existing != null)
                                {
                                    if (existing.TotalDebts != cloudDebts)
                                    {
                                        existing.TotalDebts = cloudDebts;
                                        custChanged = true;
                                    }
                                }
                                else
                                {
                                    var fullName = cc.GetProperty("full_name").GetString() ?? "عميل جديد";
                                    var phone = cc.GetProperty("primary_phone").GetString() ?? "0599000000";
                                    var licNum = cc.GetProperty("license_number").GetString() ?? natId;

                                    var newCustomer = new Customer
                                    {
                                        Id = Guid.NewGuid(),
                                        FullName = fullName,
                                        NationalIdOrPassport = natId,
                                        PrimaryPhone = phone,
                                        LicenseNumber = licNum,
                                        LicenseExpiryDate = DateTime.Today.AddYears(2),
                                        DateOfBirth = new DateTime(1995, 1, 1),
                                        Address = "طوباس",
                                        TotalDebts = cloudDebts
                                    };
                                    await context.Customers.AddAsync(newCustomer);
                                    localCustomersList.Add(newCustomer);
                                    custChanged = true;
                                }
                            }

                            if (custChanged)
                            {
                                await context.SaveChangesAsync();
                                rentalService.NotifyContractsDataChanged();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CloudSync] Background sync exception: {ex.Message}");
                }
            }
        });
    }

    // =========================================================================
    // 8. دفع كامل قاعدة البيانات الحالية إلى السحابة بنقرة واحدة (Full Push)
    // =========================================================================
    public static async Task PurgeOrphanCloudVehiclesAndCustomersAsync(CarRentalDbContext context)
    {
        if (!IsConfigured()) return;
        try
        {
            // 1. فحص وتطهير المركبات الزائدة أو الوهمية
            var localPlates = await context.Vehicles.AsNoTracking().Select(v => v.PlateNumber.Trim()).ToListAsync();
            var localPlateSet = new HashSet<string>(localPlates, StringComparer.OrdinalIgnoreCase);

            using var reqV = CreateRequest(HttpMethod.Get, "vehicles?select=id,plate_number");
            var respV = await _httpClient.SendAsync(reqV);
            if (respV.IsSuccessStatusCode)
            {
                var json = await respV.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                foreach (var elem in doc.RootElement.EnumerateArray())
                {
                    if (elem.TryGetProperty("plate_number", out var pVal) && pVal.GetString() is string plate && !string.IsNullOrWhiteSpace(plate))
                    {
                        var trimmed = plate.Trim();
                        // إذا كانت المركبة وهمية (CONC-) أو غير موجودة في أسطول المكتب المحلي
                        if (trimmed.StartsWith("CONC-", StringComparison.OrdinalIgnoreCase) || 
                            trimmed.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase) || 
                            !localPlateSet.Contains(trimmed))
                        {
                            using var delReq = CreateRequest(HttpMethod.Delete, $"vehicles?plate_number=eq.{Uri.EscapeDataString(trimmed)}");
                            await _httpClient.SendAsync(delReq);
                        }
                    }
                }
            }

            // 2. فحص وتطهير العملاء الوهميين (مثل عملاء التزامن 990000*)
            var localNationalIds = await context.Customers.AsNoTracking().Select(c => c.NationalIdOrPassport.Trim()).ToListAsync();
            var localIdSet = new HashSet<string>(localNationalIds, StringComparer.OrdinalIgnoreCase);

            using var reqC = CreateRequest(HttpMethod.Get, "customers?select=id,national_id");
            var respC = await _httpClient.SendAsync(reqC);
            if (respC.IsSuccessStatusCode)
            {
                var json = await respC.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                foreach (var elem in doc.RootElement.EnumerateArray())
                {
                    if (elem.TryGetProperty("national_id", out var nVal) && nVal.GetString() is string natId && !string.IsNullOrWhiteSpace(natId))
                    {
                        var trimmed = natId.Trim();
                        if (trimmed.StartsWith("990000", StringComparison.OrdinalIgnoreCase) || !localIdSet.Contains(trimmed))
                        {
                            using var delReq = CreateRequest(HttpMethod.Delete, $"customers?national_id=eq.{Uri.EscapeDataString(trimmed)}");
                            await _httpClient.SendAsync(delReq);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CloudSync] PurgeOrphanCloudVehiclesAndCustomers error: {ex.Message}");
        }
    }

    public static async Task<bool> PushFullDatabaseToCloudAsync(CarRentalDbContext context)
    {
        if (!IsConfigured()) return false;
        try
        {
            // تطهير سحابي أولاً
            await PurgeOrphanCloudVehiclesAndCustomersAsync(context);

            // 1. مزامنة المستخدمين
            var users = await context.Users.AsNoTracking().ToListAsync();
            foreach (var u in users)
            {
                TriggerSyncUser(u);
            }

            // 2. مزامنة المركبات
            var vehicles = await context.Vehicles.AsNoTracking().ToListAsync();
            foreach (var v in vehicles)
            {
                TriggerSyncVehicle(v);
            }

            // 3. مزامنة العملاء
            var customers = await context.Customers.AsNoTracking().ToListAsync();
            foreach (var c in customers)
            {
                TriggerSyncCustomer(c);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CloudSync] Full push error: {ex.Message}");
            return false;
        }
    }
}
