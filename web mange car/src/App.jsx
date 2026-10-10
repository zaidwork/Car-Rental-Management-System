import React, { useState, useEffect, useCallback } from 'react';
import { Car, Users, Settings, LogOut, RefreshCw, Radio, Shield, CheckCircle, Wifi, WifiOff } from 'lucide-react';
import LoginScreen from './components/LoginScreen';
import FleetView from './components/FleetView';
import CustomersView from './components/CustomersView';
import RentCarModal from './components/RentCarModal';
import { supabase, isSupabaseConfigured } from './supabaseClient';

// بيانات أولية متطابقة مع أسطول المكتب المعتمد للعمل فوراً حتى قبل إدخال مفاتيح Supabase
const INITIAL_VEHICLES = [
  {
    id: 'v1',
    plate_number: '6-1021-95',
    brand: 'هيونداي',
    model: 'أكسنت أحمر',
    model_year: 2023,
    color: 'أحمر',
    current_odometer: 25000,
    daily_standard_rate: 150,
    status: 'Available',
    image_url: '/images/cars/accent_red.webp'
  },
  {
    id: 'v2',
    plate_number: '6-0639-95',
    brand: 'هيونداي',
    model: 'أكسنت بصمة',
    model_year: 2023,
    color: 'فضي',
    current_odometer: 32000,
    daily_standard_rate: 150,
    status: 'Available',
    image_url: '/images/cars/accent_smart.jpg'
  },
  {
    id: 'v3',
    plate_number: '61',
    brand: 'هيونداي',
    model: 'أكسنت 61',
    model_year: 2022,
    color: 'رمادي فيراني',
    current_odometer: 41000,
    daily_standard_rate: 140,
    status: 'Available',
    image_url: '/images/cars/accent_61.jpg'
  },
  {
    id: 'v4',
    plate_number: '6-1022-95',
    brand: 'هيونداي',
    model: 'أكسنت أبيض',
    model_year: 2023,
    color: 'أبيض',
    current_odometer: 28000,
    daily_standard_rate: 150,
    status: 'Rented',
    image_url: '/images/cars/accent_white.jpg'
  },
  {
    id: 'v5',
    plate_number: '6-2031-95',
    brand: 'كيا',
    model: 'ستونيك (Kia Stonic)',
    model_year: 2023,
    color: 'أحمر',
    current_odometer: 21000,
    daily_standard_rate: 180,
    status: 'Available',
    image_url: '/images/cars/kia_stonic.jpg'
  },
  {
    id: 'v6',
    plate_number: '6-2032-95',
    brand: 'كيا',
    model: 'سبورتاج (Kia Sportage)',
    model_year: 2022,
    color: 'أبيض',
    current_odometer: 35000,
    daily_standard_rate: 230,
    status: 'Available',
    image_url: '/images/cars/kia_sportage.jpg'
  },
  {
    id: 'v7',
    plate_number: '6-3041-95',
    brand: 'هيونداي',
    model: 'فينيو رمادي',
    model_year: 2023,
    color: 'رمادي',
    current_odometer: 19500,
    daily_standard_rate: 160,
    status: 'Available',
    image_url: '/images/cars/venue_grey.webp'
  },
  {
    id: 'v8',
    plate_number: '6-3042-95',
    brand: 'هيونداي',
    model: 'فينيو كحلي',
    model_year: 2023,
    color: 'كحلي',
    current_odometer: 22000,
    daily_standard_rate: 160,
    status: 'Available',
    image_url: '/images/cars/venue_navy.jpg'
  },
  {
    id: 'v9',
    plate_number: '6-3043-95',
    brand: 'هيونداي',
    model: 'فينيو أسود',
    model_year: 2023,
    color: 'أسود',
    current_odometer: 18000,
    daily_standard_rate: 160,
    status: 'Available',
    image_url: '/images/cars/venue_black.webp'
  },
  {
    id: 'v10',
    plate_number: '6-3044-95',
    brand: 'هيونداي',
    model: 'فينيو خمري',
    model_year: 2023,
    color: 'خمري',
    current_odometer: 26000,
    daily_standard_rate: 160,
    status: 'Rented',
    image_url: '/images/cars/venue_burgundy.jpg'
  },
  {
    id: 'v11',
    plate_number: '6-3045-95',
    brand: 'هيونداي',
    model: 'فينيو أحمر',
    model_year: 2023,
    color: 'أحمر',
    current_odometer: 24000,
    daily_standard_rate: 160,
    status: 'Available',
    image_url: '/images/cars/venue_red.webp'
  },
  {
    id: 'v12',
    plate_number: '6-4051-95',
    brand: 'كيا',
    model: 'بيكانتو (Kia Picanto)',
    model_year: 2024,
    color: 'أبيض',
    current_odometer: 14000,
    daily_standard_rate: 120,
    status: 'Available',
    image_url: '/images/cars/kia_picanto.png'
  },
  {
    id: 'v13',
    plate_number: 'fs3',
    brand: 'هيونداي',
    model: 'أكسنت سوداء',
    model_year: 2026,
    color: 'أسود',
    current_odometer: 1000,
    daily_standard_rate: 180,
    status: 'Available',
    image_url: '/images/cars/accent_61.jpg'
  }
];

const INITIAL_CUSTOMERS = [
  {
    id: 'c1',
    full_name: 'حسين احمد راشد دراغمه',
    national_id: '411993819',
    primary_phone: '0566655550',
    license_number: 'DL-411993',
    license_expiry_date: '2028-12-31',
    date_of_birth: '1990-05-15',
    address: 'طوباس',
    total_debts: 0,
    is_blacklisted: false
  },
  {
    id: 'c2',
    full_name: 'زيد بشار فريح دراغمه',
    national_id: '409991781',
    primary_phone: '0598776026',
    license_number: 'DL-409991',
    license_expiry_date: '2028-06-15',
    date_of_birth: '1998-08-20',
    address: 'طوباس',
    total_debts: 80,
    is_blacklisted: false
  },
  {
    id: 'c3',
    full_name: 'محمد لؤي محمد ابو دواس',
    national_id: '409996816',
    primary_phone: '0598733510',
    license_number: 'DL-409996',
    license_expiry_date: '2029-01-10',
    date_of_birth: '1995-11-12',
    address: 'طوباس',
    total_debts: 0,
    is_blacklisted: false
  },
  {
    id: 'c4',
    full_name: 'احمد عنان حسني دراغمة',
    national_id: '40998632',
    primary_phone: '059976532',
    license_number: 'DL-409986',
    license_expiry_date: '2027-09-30',
    date_of_birth: '1994-03-10',
    address: 'طوباس',
    total_debts: 0,
    is_blacklisted: false
  },
  {
    id: 'c5',
    full_name: 'وسيم محمد ابراهيم عبد الرازق',
    national_id: '4099765312',
    primary_phone: '05997653110',
    license_number: 'DL-883NFUCN',
    license_expiry_date: '2028-09-23',
    date_of_birth: '1996-03-15',
    address: 'طوباس الثعرة',
    total_debts: 0,
    is_blacklisted: false
  }
];

const INITIAL_CONTRACTS = [
  {
    id: 'cnt1',
    contract_number: 'CR-2026-0012',
    vehicle_id: 'v4', // أكسنت أبيض
    customer_id: 'c2', // زيد دراغمه
    start_date_time: new Date(Date.now() - 36 * 3600 * 1000).toISOString(),
    expected_return_date_time: new Date(Date.now() + 12 * 3600 * 1000).toISOString(),
    daily_agreed_rate: 150,
    total_agreed_amount: 300,
    paid_amount: 220,
    remaining_amount: 80,
    status: 'Active',
    payment_mode: 'ImmediateCash'
  },
  {
    id: 'cnt2',
    contract_number: 'CR-2026-0013',
    vehicle_id: 'v10', // فينيو خمري
    customer_id: 'c3', // محمد أبو دواس
    start_date_time: new Date(Date.now() - 10 * 3600 * 1000).toISOString(),
    expected_return_date_time: new Date(Date.now() + 38 * 3600 * 1000).toISOString(),
    daily_agreed_rate: 160,
    total_agreed_amount: 320,
    paid_amount: 320,
    remaining_amount: 0,
    status: 'Active',
    payment_mode: 'ImmediateCash'
  }
];

export default function App() {
  const [currentUser, setCurrentUser] = useState(() => {
    // استخدام sessionStorage لجلسة الدخول المؤقتة بحيث يُطلب الرمز السري عند فتح الموقع من جديد
    localStorage.removeItem('CAR_RENTAL_ACTIVE_USER');
    const saved = sessionStorage.getItem('CAR_RENTAL_ACTIVE_USER');
    return saved ? JSON.parse(saved) : null;
  });

  const [activeTab, setActiveTab] = useState('fleet'); // 'fleet', 'customers'
  const [vehicles, setVehicles] = useState(INITIAL_VEHICLES);
  const [customers, setCustomers] = useState(INITIAL_CUSTOMERS);
  const [contracts, setContracts] = useState(INITIAL_CONTRACTS);

  const [isLoading, setIsLoading] = useState(false);
  const [isCloudConnected, setIsCloudConnected] = useState(false);
  const [isRealtimeLive, setIsRealtimeLive] = useState(false);

  // حالة مودال التأجير
  const [isRentModalOpen, setIsRentModalOpen] = useState(false);
  const [targetVehicleId, setTargetVehicleId] = useState('');
  const [targetCustomerId, setTargetCustomerId] = useState('');

  // حالة مودال إعدادات Supabase
  const [isConfigModalOpen, setIsConfigModalOpen] = useState(false);

  // حالة تثبيت تطبيق PWA على الهاتف
  const [installPrompt, setInstallPrompt] = useState(null);

  useEffect(() => {
    const handleBeforeInstallPrompt = (e) => {
      e.preventDefault();
      setInstallPrompt(e);
    };
    window.addEventListener('beforeinstallprompt', handleBeforeInstallPrompt);
    return () => window.removeEventListener('beforeinstallprompt', handleBeforeInstallPrompt);
  }, []);

  const handleInstallApp = async () => {
    if (!installPrompt) return;
    installPrompt.prompt();
    const { outcome } = await installPrompt.userChoice;
    if (outcome === 'accepted') {
      setInstallPrompt(null);
    }
  };

  // التحقق الفوري من صلاحية الجلسة الحالية مع قاعدة البيانات الحية
  useEffect(() => {
    localStorage.removeItem('CAR_RENTAL_ACTIVE_USER');
    if (currentUser && supabase) {
      supabase
        .from('users')
        .select('username, full_name, role, is_active')
        .eq('username', currentUser.username)
        .maybeSingle()
        .then(({ data, error }) => {
          if (!error && (!data || !data.is_active)) {
            handleLogout();
          } else if (data) {
            const updated = { ...currentUser, fullName: data.full_name, role: data.role };
            setCurrentUser(updated);
            sessionStorage.setItem('CAR_RENTAL_ACTIVE_USER', JSON.stringify(updated));
          }
        });
    }
  }, []);

  // جلب البيانات من سوبابيز بالتوازي فائق السرعة
  const fetchData = useCallback(async (isSilent = false) => {
    if (!isSilent) setIsLoading(true);
    try {
      const configured = isSupabaseConfigured();
      setIsCloudConnected(configured);

      if (configured && supabase) {
        // استدعاء متوازي فوري (Parallel Fetching) لتقليل زمن الاستجابة إلى أجزاء من الثانية
        const [vRes, cRes, cntRes, uRes] = await Promise.all([
          supabase.from('vehicles').select('*').order('plate_number', { ascending: true }),
          supabase.from('customers').select('*').order('full_name', { ascending: true }),
          supabase.from('contracts').select('*').eq('status', 'Active'),
          supabase.from('users').select('username, full_name, role, pin_code, is_active').eq('is_active', true)
        ]);

        if (!vRes.error && vRes.data) {
          // استبعاد وحجب أي مركبات وهمية تجريبية (مثل CONC- أو TEST-) أو غير نشطة
          const validVehicles = vRes.data.filter(v => 
            v.is_active !== false &&
            !v.plate_number?.toUpperCase().startsWith('CONC-') &&
            !v.plate_number?.toUpperCase().startsWith('TEST-')
          );
          setVehicles(validVehicles.length > 0 ? validVehicles : INITIAL_VEHICLES);
        }

        if (!cRes.error && cRes.data) {
          // استبعاد وحجب أي عملاء وهميين تجريبيين من فحوصات التزامن (مثل 990000*)
          const validCustomers = cRes.data.filter(c => 
            !c.national_id?.startsWith('990000') &&
            !c.full_name?.includes('عميل التزامن')
          );
          setCustomers(validCustomers.length > 0 ? validCustomers : INITIAL_CUSTOMERS);
        }

        if (!cntRes.error && cntRes.data) {
          setContracts(cntRes.data);
        }

        if (!uRes.error && uRes.data && uRes.data.length > 0) {
          localStorage.setItem('CACHED_SYSTEM_USERS', JSON.stringify(uRes.data.map(u => ({
            username: u.username,
            fullName: u.full_name,
            role: u.role,
            pin: u.pin_code
          }))));
        }
      }
    } catch (err) {
      console.warn('Could not sync with Supabase, using internal cache:', err);
    } finally {
      if (!isSilent) setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (currentUser) {
      // جلب فوري عند فتح الشاشة
      fetchData(false);

      let channel = null;
      let isDisposed = false;

      // 1. محرك الاستماع اللحظي فائق السرعة عبر WebSockets مع إعادة الاتصال التلقائي
      const initRealtime = () => {
        if (!isSupabaseConfigured() || !supabase || isDisposed) return;
        if (channel) {
          try { supabase.removeChannel(channel); } catch { }
        }

        channel = supabase
          .channel(`car-rental-live-${Date.now()}`)
          .on('postgres_changes', { event: '*', schema: 'public', table: 'vehicles' }, (payload) => {
            if (payload?.eventType === 'DELETE' && payload.old) {
              setVehicles(prev => prev.filter(v => v.id !== payload.old.id && v.plate_number !== payload.old.plate_number));
            } else if (payload?.eventType === 'INSERT' && payload.new) {
              const newPlate = (payload.new.plate_number || '').toUpperCase();
              if (!newPlate.startsWith('CONC-') && !newPlate.startsWith('TEST-') && payload.new.is_active !== false) {
                setVehicles(prev => {
                  if (prev.some(v => v.id === payload.new.id || v.plate_number === payload.new.plate_number)) {
                    return prev.map(v => (v.id === payload.new.id || v.plate_number === payload.new.plate_number) ? { ...v, ...payload.new } : v);
                  }
                  return [...prev, payload.new];
                });
              }
            } else if (payload?.new) {
              const newPlate = (payload.new.plate_number || '').toUpperCase();
              if (newPlate.startsWith('CONC-') || newPlate.startsWith('TEST-')) return;
              setVehicles(prev => prev.map(v => 
                (v.id === payload.new.id || v.plate_number === payload.new.plate_number || (payload.old?.plate_number && v.plate_number === payload.old.plate_number)) 
                  ? { ...v, ...payload.new } 
                  : v
              ));
            }
            fetchData(true);
          })
          .on('postgres_changes', { event: '*', schema: 'public', table: 'contracts' }, (payload) => {
            if (payload?.eventType === 'INSERT' && payload.new) {
              setContracts(prev => {
                if (prev.some(c => c.id === payload.new.id || c.contract_number === payload.new.contract_number)) return prev;
                return [payload.new, ...prev];
              });
            } else if (payload?.eventType === 'UPDATE' && payload.new) {
              setContracts(prev => prev.map(c => 
                (c.id === payload.new.id || c.contract_number === payload.new.contract_number) 
                  ? { ...c, ...payload.new } 
                  : c
              ));
            }
            fetchData(true);
          })
          .on('postgres_changes', { event: '*', schema: 'public', table: 'customers' }, (payload) => {
            if (payload?.new) {
              setCustomers(prev => prev.map(c => 
                (c.id === payload.new.id || c.national_id === payload.new.national_id) 
                  ? { ...c, ...payload.new } 
                  : c
              ));
            }
            fetchData(true);
          })
          .on('postgres_changes', { event: '*', schema: 'public', table: 'users' }, (payload) => {
            if (payload?.eventType === 'DELETE' && payload.old?.username === currentUser?.username) {
              handleLogout();
            } else if (payload?.new && payload.new.username === currentUser?.username) {
              if (!payload.new.is_active) {
                handleLogout();
              } else {
                const updatedUser = {
                  ...currentUser,
                  fullName: payload.new.full_name,
                  role: payload.new.role
                };
                setCurrentUser(updatedUser);
                sessionStorage.setItem('CAR_RENTAL_ACTIVE_USER', JSON.stringify(updatedUser));
              }
            }
            fetchData(true);
          })
          .subscribe((status) => {
            if (status === 'SUBSCRIBED') {
              setIsRealtimeLive(true);
            } else if (status === 'CLOSED' || status === 'CHANNEL_ERROR') {
              setIsRealtimeLive(false);
              if (!isDisposed) {
                setTimeout(initRealtime, 2500);
              }
            }
          });
      };

      initRealtime();

      // 2. تحديث تلقائي فوري عند العودة للمتصفح أو إضاءة شاشة الهاتف
      const handleVisibilityChange = () => {
        if (document.visibilityState === 'visible') {
          fetchData(true);
          initRealtime();
        }
      };
      window.addEventListener('visibilitychange', handleVisibilityChange);
      window.addEventListener('focus', () => fetchData(true));

      // 3. نبض احتياطي خفيف (كل 45 ثانية فقط عندما تكون الشاشة مفتوحة) لضمان حماية باقة سوبابيز المجانية
      // التزامن الفوري اللحظي يتم عبر WebSockets في أجزاء من الثانية دون أي استهلاك إضافي
      const pulseInterval = setInterval(() => {
        if (typeof document !== 'undefined' && document.visibilityState === 'visible') {
          fetchData(true);
        }
      }, 45000);

      return () => {
        isDisposed = true;
        if (channel && supabase) {
          try { supabase.removeChannel(channel); } catch { }
        }
        window.removeEventListener('visibilitychange', handleVisibilityChange);
        window.removeEventListener('focus', () => fetchData(true));
        clearInterval(pulseInterval);
      };
    }
  }, [currentUser, fetchData]);

  const handleLoginSuccess = (user) => {
    setCurrentUser(user);
    sessionStorage.setItem('CAR_RENTAL_ACTIVE_USER', JSON.stringify(user));
    localStorage.removeItem('CAR_RENTAL_ACTIVE_USER');
  };

  const handleLogout = () => {
    setCurrentUser(null);
    sessionStorage.removeItem('CAR_RENTAL_ACTIVE_USER');
    localStorage.removeItem('CAR_RENTAL_ACTIVE_USER');
  };

  // فتح نافذة التأجير لسيارة معينة
  const handleOpenRentForCar = (vehicle) => {
    setTargetVehicleId(vehicle.id);
    setTargetCustomerId('');
    setIsRentModalOpen(true);
  };

  // فتح نافذة التأجير لعميل معين
  const handleOpenRentForCustomer = (customer) => {
    setTargetCustomerId(customer.id);
    setTargetVehicleId('');
    setIsRentModalOpen(true);
  };

  // عند نجاح عملية التأجير
  const handleRentSuccess = (newContract) => {
    // تحديث محلي فوري للمركبة
    setVehicles(prev => prev.map(v => {
      if (v.id === newContract.vehicle_id) {
        return { ...v, status: 'Rented' };
      }
      return v;
    }));

    // تحديث العقود
    setContracts(prev => [newContract, ...prev]);

    // تحديث رصيد ديون العميل فورياً في الذاكرة
    if (newContract.remaining_amount > 0) {
      setCustomers(prev => prev.map(c => {
        if (c.id === newContract.customer_id) {
          return { ...c, total_debts: Number(c.total_debts || 0) + Number(newContract.remaining_amount) };
        }
        return c;
      }));
    }

    // إعادة الجلب التلقائي الصامت من Supabase
    fetchData(true);
  };

  const handleDeleteCustomer = async (customer) => {
    if (!window.confirm(`هل أنت متأكد من حذف العميل "${customer.full_name}" نهائياً من النظام؟`)) return;
    try {
      await supabase.from('blacklist_records').delete().eq('national_id', customer.national_id);
      const { error } = await supabase.from('customers').delete().eq('id', customer.id);
      if (error) throw error;
      setCustomers(prev => prev.filter(c => c.id !== customer.id));
    } catch (err) {
      alert('حدث خطأ أثناء حذف العميل: ' + err.message);
    }
  };

  // إذا لم يسجل الدخول، اعرض شاشة الـ PIN
  if (!currentUser) {
    return (
      <LoginScreen
        onLoginSuccess={handleLoginSuccess}
        installPrompt={installPrompt}
        onInstallClick={handleInstallApp}
      />
    );
  }

  return (
    <div className="mobile-container">
      {/* 1. الشريط العلوي الفاخر (Header - مطابق لـ TopHeader.razor) */}
      <header className="mobile-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <img
            src="/images/logo.png"
            alt="TAJ SYSTEM"
            className="brand-logo"
            onError={(e) => {
              e.target.style.display = 'none';
            }}
          />
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <h1 className="header-title" style={{ fontSize: '1.05rem', margin: 0, color: 'var(--wood-walnut)', fontWeight: 900 }}>
                TAJ SYSTEM
              </h1>
              <div style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '5px',
                background: isRealtimeLive ? '#dcfce7' : '#ffedd5',
                color: isRealtimeLive ? '#166534' : '#9a3412',
                border: `1px solid ${isRealtimeLive ? '#86efac' : '#fdba74'}`,
                padding: '2px 8px',
                borderRadius: '12px',
                fontSize: '0.65rem',
                fontWeight: 900,
                boxShadow: isRealtimeLive ? '0 0 10px rgba(34, 197, 94, 0.2)' : 'none'
              }}>
                <span style={{
                  width: '6px',
                  height: '6px',
                  borderRadius: '50%',
                  background: isRealtimeLive ? '#16a34a' : '#ea580c',
                  display: 'inline-block'
                }}></span>
                <span>{isRealtimeLive ? 'تزامن لحظي فوري ✓' : 'جاري الاتصال...'}</span>
              </div>
            </div>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)', fontWeight: 700, marginTop: '2px' }}>
              <span>{currentUser.role === 'Admin' ? '👑 المدير: ' : '🧑‍💼 الموظف: '}</span>
              <strong style={{ color: 'var(--wood-walnut)' }}>{currentUser.fullName}</strong>
            </div>
          </div>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          {/* زر التحديث السريع */}
          <button
            onClick={fetchData}
            disabled={isLoading}
            style={{
              background: '#ffffff',
              border: '1.5px solid var(--border-color)',
              borderRadius: '50%',
              width: '36px',
              height: '36px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'var(--wood-walnut)',
              cursor: 'pointer',
              boxShadow: '0 2px 6px rgba(45, 24, 12, 0.05)'
            }}
            title="تحديث البيانات فورياً"
          >
            <RefreshCw size={15} className={isLoading ? 'spin' : ''} />
          </button>

          {/* زر تسجيل الخروج / قفل */}
          <button
            onClick={handleLogout}
            style={{
              background: '#fff5f5',
              border: '1.5px solid #fca5a5',
              borderRadius: '20px',
              padding: '6px 12px',
              display: 'flex',
              alignItems: 'center',
              gap: '4px',
              color: '#dc2626',
              fontSize: '0.76rem',
              fontWeight: 800,
              cursor: 'pointer',
              boxShadow: '0 2px 6px rgba(220, 38, 38, 0.08)'
            }}
            title="تسجيل الخروج والعودة لشاشة الـ PIN"
          >
            <LogOut size={14} />
            <span>قفل</span>
          </button>
        </div>
      </header>

      {/* شريط دعوة تثبيت التطبيق على الهاتف PWA */}
      {installPrompt && (
        <div style={{
          background: 'linear-gradient(135deg, #2d180c 0%, #442211 100%)',
          color: '#ffffff',
          padding: '8px 14px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          borderBottom: '1.5px solid var(--wood-gold-inlay)',
          boxShadow: '0 4px 14px rgba(45, 24, 12, 0.2)',
          zIndex: 90
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <img
              src="/images/logo_192.png"
              alt="TAJ"
              style={{ width: '28px', height: '28px', borderRadius: '50%', border: '1px solid #d4af37' }}
            />
            <span style={{ fontSize: '0.78rem', fontWeight: 800 }}>تطبيق التاج متاح للتثبيت على هاتفك</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <button
              onClick={handleInstallApp}
              style={{
                background: 'linear-gradient(135deg, #d4af37 0%, #c59b27 100%)',
                color: '#1e110a',
                border: 'none',
                padding: '4px 12px',
                borderRadius: '12px',
                fontWeight: 900,
                fontSize: '0.72rem',
                cursor: 'pointer'
              }}
            >
              📲 تثبيت
            </button>
            <button
              onClick={() => setInstallPrompt(null)}
              style={{ background: 'transparent', border: 'none', color: '#ded4bf', cursor: 'pointer', fontSize: '1.1rem', lineHeight: 1 }}
              title="إغلاق"
            >
              ×
            </button>
          </div>
        </div>
      )}

      {/* 2. محتوى الصفحة حسب التبويب النشط */}
      <main style={{ paddingBottom: '90px' }}>
        {activeTab === 'fleet' && (
          <FleetView
            vehicles={vehicles}
            contracts={contracts}
            customers={customers}
            onRentCarClick={handleOpenRentForCar}
          />
        )}

        {activeTab === 'customers' && (
          <CustomersView
            customers={customers}
            onRentForCustomer={handleOpenRentForCustomer}
            currentUser={currentUser}
            onDeleteCustomer={handleDeleteCustomer}
          />
        )}
      </main>

      {/* 3. شريط التنقل السفلي للهواتف (Bottom Navigation Bar) */}
      <nav className="bottom-nav">
        <button
          className={`bottom-nav-item ${activeTab === 'fleet' ? 'active' : ''}`}
          onClick={() => setActiveTab('fleet')}
        >
          <Car size={22} />
          <span>الأسطول والسيارات</span>
        </button>

        <button
          className={`bottom-nav-item ${activeTab === 'customers' ? 'active' : ''}`}
          onClick={() => setActiveTab('customers')}
        >
          <Users size={22} />
          <span>سجل العملاء</span>
        </button>

        <button
          className="bottom-nav-item"
          onClick={fetchData}
          disabled={isLoading}
        >
          <RefreshCw size={22} className={isLoading ? 'spin' : ''} />
          <span>تحديث فوري</span>
        </button>
      </nav>

      {/* 4. نافذة تأجير السيارة للعملاء المسجلين فقط */}
      <RentCarModal
        isOpen={isRentModalOpen}
        onClose={() => setIsRentModalOpen(false)}
        vehicles={vehicles}
        customers={customers}
        initialVehicleId={targetVehicleId}
        initialCustomerId={targetCustomerId}
        currentUser={currentUser}
        onRentSuccess={handleRentSuccess}
      />
    </div>
  );
}
