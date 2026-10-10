-- ===============================================================================
-- سكريبت إنشاء وتهيئة قاعدة بيانات نظام تأجير السيارات على منصة Supabase
-- Car Rental System - Supabase PostgreSQL Schema & Security Policies
-- ===============================================================================

-- 1. تفعيل ملحق توليد معرّفات UUID
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ===============================================================================
-- 2. جدول إعدادات وهوية الشركة (Company Profile)
-- ===============================================================================
CREATE TABLE IF NOT EXISTS company_profile (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_name TEXT NOT NULL DEFAULT 'شركة تأجير السيارات',
    company_english_name TEXT NOT NULL DEFAULT 'Car Rental Co.',
    commercial_register_number TEXT NOT NULL DEFAULT '000000',
    tax_number TEXT NULL,
    primary_phone TEXT NOT NULL DEFAULT '0599000000',
    secondary_phone TEXT NULL,
    address TEXT NOT NULL DEFAULT 'فلسطين',
    email TEXT NULL,
    logo_url TEXT NULL,
    default_contract_terms TEXT NOT NULL DEFAULT 'شروط العقد الافتراضية',
    daily_km_limit INTEGER NOT NULL DEFAULT 250,
    extra_km_price NUMERIC NOT NULL DEFAULT 1.0,
    late_hour_fee NUMERIC NOT NULL DEFAULT 20.0,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===============================================================================
-- 3. جدول المستخدمين والموظفين (Users & Staff)
-- ===============================================================================
CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    username TEXT UNIQUE NOT NULL,
    full_name TEXT NOT NULL,
    pin_code TEXT NOT NULL, -- رمز الدخول المكون من 4 أرقام
    role TEXT NOT NULL CHECK (role IN ('Admin', 'Staff', 'Accountant')),
    phone_number TEXT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- دالة وقيد حماية المدير العام الوحيد النشط لمنع حظر النظام
CREATE OR REPLACE FUNCTION protect_sole_active_admin()
RETURNS TRIGGER AS $$
DECLARE
    other_active_admins_count INT;
BEGIN
    IF (TG_OP = 'UPDATE') THEN
        IF (OLD.role = 'Admin' AND OLD.is_active = TRUE) AND (NEW.is_active = FALSE OR NEW.role != 'Admin') THEN
            SELECT COUNT(*) INTO other_active_admins_count
            FROM users
            WHERE role = 'Admin' AND is_active = TRUE AND id != OLD.id;

            IF (other_active_admins_count = 0) THEN
                RAISE EXCEPTION 'لا يمكن تعطيل أو تغيير رتبة المدير العام (الآدمن) الوحيد النشط في النظام! يجب توفير مدير عام آخر نشط أولاً.';
            END IF;
        END IF;
        RETURN NEW;
    ELSIF (TG_OP = 'DELETE') THEN
        IF (OLD.role = 'Admin' AND OLD.is_active = TRUE) THEN
            SELECT COUNT(*) INTO other_active_admins_count
            FROM users
            WHERE role = 'Admin' AND is_active = TRUE AND id != OLD.id;

            IF (other_active_admins_count = 0) THEN
                RAISE EXCEPTION 'لا يمكن حذف حساب المدير العام الوحيد النشط في النظام! يجب توفير مدير عام آخر نشط أولاً.';
            END IF;
        END IF;
        RETURN OLD;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_protect_sole_active_admin ON users;
CREATE TRIGGER trg_protect_sole_active_admin
BEFORE UPDATE OR DELETE ON users
FOR EACH ROW
EXECUTE FUNCTION protect_sole_active_admin();

-- ===============================================================================
-- 4. جدول العملاء المسجلين (Customers)
-- ===============================================================================
CREATE TABLE IF NOT EXISTS customers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    full_name TEXT NOT NULL,
    national_id TEXT UNIQUE NOT NULL,
    primary_phone TEXT NOT NULL,
    emergency_phone TEXT NULL,
    license_number TEXT NOT NULL,
    license_expiry_date DATE NOT NULL,
    date_of_birth DATE NOT NULL,
    address TEXT NOT NULL,
    total_debts NUMERIC NOT NULL DEFAULT 0.0,
    notes TEXT NULL,
    created_by TEXT NOT NULL DEFAULT 'admin',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===============================================================================
-- 5. جدول القائمة السوداء (Blacklist Records)
-- ===============================================================================
CREATE TABLE IF NOT EXISTS blacklist_records (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID REFERENCES customers(id) ON DELETE CASCADE,
    national_id TEXT NOT NULL,
    reason TEXT NOT NULL,
    is_blocked BOOLEAN NOT NULL DEFAULT TRUE,
    blocked_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    blocked_by TEXT NOT NULL DEFAULT 'admin',
    unblocked_at TIMESTAMPTZ NULL,
    unblock_reason TEXT NULL,
    unblocked_by TEXT NULL
);

-- ===============================================================================
-- 6. جدول أسطول المركبات (Vehicles Fleet)
-- ===============================================================================
CREATE TABLE IF NOT EXISTS vehicles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    plate_number TEXT UNIQUE NOT NULL,
    brand TEXT NOT NULL,
    model TEXT NOT NULL,
    model_year INTEGER NOT NULL,
    color TEXT NOT NULL,
    vin_number TEXT NULL,
    current_odometer INTEGER NOT NULL DEFAULT 0,
    daily_standard_rate NUMERIC NOT NULL DEFAULT 150.0,
    status TEXT NOT NULL DEFAULT 'Available' CHECK (status IN ('Available', 'Rented', 'InMaintenance', 'Reserved')),
    license_expiry_date DATE NULL,
    insurance_expiry_date DATE NULL,
    insurance_company TEXT NULL,
    next_oil_change_odometer INTEGER NULL,
    next_maintenance_date DATE NULL,
    is_maintenance_critical BOOLEAN NOT NULL DEFAULT FALSE,
    critical_maintenance_reason TEXT NULL,
    image_url TEXT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===============================================================================
-- 7. جدول عقود التأجير (Contracts)
-- ===============================================================================
CREATE TABLE IF NOT EXISTS contracts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    contract_number TEXT UNIQUE NOT NULL,
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE RESTRICT,
    vehicle_id UUID NOT NULL REFERENCES vehicles(id) ON DELETE RESTRICT,
    start_date_time TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expected_return_date_time TIMESTAMPTZ NOT NULL,
    actual_return_date_time TIMESTAMPTZ NULL,
    is_open_ended BOOLEAN NOT NULL DEFAULT FALSE,
    daily_agreed_rate NUMERIC NOT NULL,
    total_agreed_amount NUMERIC NOT NULL DEFAULT 0.0,
    paid_amount NUMERIC NOT NULL DEFAULT 0.0,
    remaining_amount NUMERIC NOT NULL DEFAULT 0.0,
    payment_mode TEXT NOT NULL DEFAULT 'ImmediateCash' CHECK (payment_mode IN ('ImmediateCash', 'DeferredOnReturn', 'BankTransfer', 'CreditCard')),
    status TEXT NOT NULL DEFAULT 'Active' CHECK (status IN ('Active', 'Closed', 'Cancelled')),
    promissory_note_amount NUMERIC NOT NULL DEFAULT 10000.0,
    promissory_note_number TEXT NULL,
    start_odometer INTEGER NOT NULL,
    end_odometer INTEGER NULL,
    created_by_staff TEXT NOT NULL DEFAULT 'admin',
    closed_by_staff TEXT NULL,
    notes TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===============================================================================
-- 8. جدول كمبيالات الأمانة والضمان (Promissory Notes)
-- ===============================================================================
CREATE TABLE IF NOT EXISTS promissory_notes (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    note_number TEXT UNIQUE NOT NULL,
    contract_id UUID NOT NULL REFERENCES contracts(id) ON DELETE CASCADE,
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE RESTRICT,
    amount NUMERIC NOT NULL,
    issue_date DATE NOT NULL DEFAULT CURRENT_DATE,
    due_date DATE NOT NULL,
    status TEXT NOT NULL DEFAULT 'HeldInTrust' CHECK (status IN ('HeldInTrust', 'ReleasedToClient', 'ExecutedLegal')),
    legal_text TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===============================================================================
-- 9. جدول الحركات المالية (Financial Transactions)
-- ===============================================================================
CREATE TABLE IF NOT EXISTS financial_transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    receipt_number TEXT UNIQUE NOT NULL,
    contract_id UUID NULL REFERENCES contracts(id) ON DELETE SET NULL,
    customer_id UUID NULL REFERENCES customers(id) ON DELETE SET NULL,
    vehicle_id UUID NULL REFERENCES vehicles(id) ON DELETE SET NULL,
    type TEXT NOT NULL CHECK (type IN ('Receipt', 'Payment')),
    category TEXT NOT NULL CHECK (category IN ('RentalPayment', 'ContractExtension', 'DebtPayment', 'Refund', 'TrafficViolation', 'GeneralExpense', 'VehicleMaintenance')),
    amount NUMERIC NOT NULL CHECK (amount > 0),
    payment_method TEXT NOT NULL DEFAULT 'ImmediateCash',
    staff_username TEXT NOT NULL,
    transaction_date_time TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    description TEXT NOT NULL
);

-- ===============================================================================
-- 10. جدول سجل الرقابة والتدقيق (Audit Logs)
-- ===============================================================================
CREATE TABLE IF NOT EXISTS audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    username TEXT NOT NULL,
    role TEXT NOT NULL,
    action TEXT NOT NULL,
    entity_name TEXT NOT NULL,
    record_id TEXT NOT NULL,
    details TEXT NOT NULL,
    client_device TEXT NULL DEFAULT 'Mobile Web',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===============================================================================
-- 11. الفهارس الفائقة لتسريع الاستعلامات والبحث اللحظي
-- ===============================================================================
CREATE INDEX IF NOT EXISTS idx_vehicles_status ON vehicles(status);
CREATE INDEX IF NOT EXISTS idx_contracts_status ON contracts(status);
CREATE INDEX IF NOT EXISTS idx_contracts_vehicle_id ON contracts(vehicle_id);
CREATE INDEX IF NOT EXISTS idx_contracts_customer_id ON contracts(customer_id);
CREATE INDEX IF NOT EXISTS idx_customers_national_id ON customers(national_id);
CREATE INDEX IF NOT EXISTS idx_blacklist_national_id ON blacklist_records(national_id);
CREATE INDEX IF NOT EXISTS idx_transactions_created_at ON financial_transactions(transaction_date_time);

-- ===============================================================================
-- 12. دالة التأجير الذرية المقفلة لمنع أي تضارب (Atomic Conflict-Free Rental)
-- ===============================================================================
-- هذه الدالة تضمن 100% أن السيارة لا تؤجر مرتين في نفس اللحظة
-- فإذا حاول الموبايل والسيستم المكتبي تأجيرها بنفس الثانية، أحدهما ينجح والآخر يفشل فوراً
CREATE OR REPLACE FUNCTION rent_vehicle_atomic(
    p_customer_id UUID,
    p_vehicle_id UUID,
    p_rental_days INTEGER,
    p_start_date TIMESTAMPTZ,
    p_expected_return TIMESTAMPTZ,
    p_is_open_ended BOOLEAN,
    p_daily_rate NUMERIC,
    p_total_amount NUMERIC,
    p_paid_amount NUMERIC,
    p_remaining_amount NUMERIC,
    p_payment_mode TEXT,
    p_promissory_amount NUMERIC,
    p_staff_username TEXT,
    p_notes TEXT
)
RETURNS JSONB
LANGUAGE plpgsql
AS $$
DECLARE
    v_vehicle_status TEXT;
    v_vehicle_plate TEXT;
    v_vehicle_odo INTEGER;
    v_customer_blocked BOOLEAN;
    v_customer_name TEXT;
    v_new_contract_id UUID := uuid_generate_v4();
    v_contract_no TEXT;
    v_receipt_no TEXT;
    v_note_no TEXT;
    v_seq_num INTEGER;
BEGIN
    -- 1. فحص وحجز السيارة ذرياً (Atomic Lock Check)
    SELECT status, plate_number, current_odometer 
    INTO v_vehicle_status, v_vehicle_plate, v_vehicle_odo
    FROM vehicles 
    WHERE id = p_vehicle_id 
    FOR UPDATE; -- قفل السطر لمنع أي تضارب متزامن

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'message', 'المركبة غير موجودة بالنظام!');
    END IF;

    IF v_vehicle_status <> 'Available' THEN
        RETURN jsonb_build_object('success', false, 'message', 'عذراً! المركبة أصبحت مؤجرة أو قيد الصيانة للتو ولا يمكن تأجيرها.');
    END IF;

    -- 2. التحقق من العميل والقائمة السوداء
    SELECT c.full_name, COALESCE(b.is_blocked, FALSE)
    INTO v_customer_name, v_customer_blocked
    FROM customers c
    LEFT JOIN blacklist_records b ON b.national_id = c.national_id AND b.is_blocked = TRUE
    WHERE c.id = p_customer_id;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'message', 'المستأجر غير مسجل في النظام!');
    END IF;

    IF v_customer_blocked THEN
        RETURN jsonb_build_object('success', false, 'message', 'تنبيه أمني: العميل مدرج في القائمة السوداء ويمنع تأجيره!');
    END IF;

    -- 3. توليد أرقام تسلسلية مميزة للموبايل (بادئة CRM لمنع التصادم مع المكتب)
    SELECT COUNT(*) + 1 INTO v_seq_num FROM contracts;
    v_contract_no := 'CRM-' || TO_CHAR(NOW(), 'YYYY') || '-' || LPAD(v_seq_num::TEXT, 4, '0');
    v_receipt_no := 'REC-M' || TO_CHAR(NOW(), 'YYYYMMDD') || '-' || LPAD(v_seq_num::TEXT, 4, '0');
    v_note_no := 'PN-M' || TO_CHAR(NOW(), 'YYYY') || '-' || LPAD(v_seq_num::TEXT, 4, '0');

    -- 4. إدراج العقد
    INSERT INTO contracts (
        id, contract_number, customer_id, vehicle_id, start_date_time, expected_return_date_time,
        is_open_ended, daily_agreed_rate, total_agreed_amount, paid_amount, remaining_amount,
        payment_mode, status, promissory_note_amount, promissory_note_number, start_odometer,
        created_by_staff, notes
    ) VALUES (
        v_new_contract_id, v_contract_no, p_customer_id, p_vehicle_id, p_start_date, p_expected_return,
        p_is_open_ended, p_daily_rate, p_total_amount, p_paid_amount, p_remaining_amount,
        p_payment_mode, 'Active', p_promissory_amount, v_note_no, v_vehicle_odo,
        p_staff_username, p_notes
    );

    -- 5. تحديث حالة المركبة فوراً إلى مؤجرة
    UPDATE vehicles 
    SET status = 'Rented' 
    WHERE id = p_vehicle_id;

    -- 6. إنشاء كمبيالة الضمان
    INSERT INTO promissory_notes (
        note_number, contract_id, customer_id, amount, issue_date, due_date, status, legal_text
    ) VALUES (
        v_note_no, v_new_contract_id, p_customer_id, p_promissory_amount, CURRENT_DATE, p_expected_return::DATE,
        'HeldInTrust', 'كمبيالة ضمان للمركبة ' || v_vehicle_plate
    );

    -- 7. ترحيل الذمم إلى دين العميل إن وجد متبقي
    IF p_remaining_amount > 0 THEN
        UPDATE customers 
        SET total_debts = total_debts + p_remaining_amount 
        WHERE id = p_customer_id;
    END IF;

    -- 8. تسجيل سند القبض المالي إن تم دفع مبلغ
    IF p_paid_amount > 0 THEN
        INSERT INTO financial_transactions (
            receipt_number, contract_id, customer_id, vehicle_id, type, category,
            amount, payment_method, staff_username, description
        ) VALUES (
            v_receipt_no, v_new_contract_id, p_customer_id, p_vehicle_id, 'Receipt', 'RentalPayment',
            p_paid_amount, p_payment_mode, p_staff_username,
            'دفعة عقد تأجير ' || v_contract_no || ' لسيارة ' || v_vehicle_plate || ' من تطبيق الهاتف'
        );
    END IF;

    -- 9. تسجيل التدقيق الأمني
    INSERT INTO audit_logs (
        username, role, action, entity_name, record_id, details, client_device
    ) VALUES (
        p_staff_username, 'Staff', 'Insert', 'Contracts', v_new_contract_id::TEXT,
        'فتح عقد تأجير من تطبيق الموبايل ' || v_contract_no || ' للمستأجر ' || v_customer_name || ' بمبلغ ' || p_total_amount || ' ₪',
        'Mobile Web'
    );

    RETURN jsonb_build_object(
        'success', true, 
        'contract_id', v_new_contract_id, 
        'contract_number', v_contract_no, 
        'message', 'تم تأجير المركبة بنجاح وإصدار العقد والكمبيالة فورياً'
    );
END;
$$;

-- ===============================================================================
-- 13. تفعيل الأمان وسياسات الوصول Row Level Security (RLS)
-- ===============================================================================
ALTER TABLE company_profile ENABLE ROW LEVEL SECURITY;
ALTER TABLE users ENABLE ROW LEVEL SECURITY;
ALTER TABLE customers ENABLE ROW LEVEL SECURITY;
ALTER TABLE blacklist_records ENABLE ROW LEVEL SECURITY;
ALTER TABLE vehicles ENABLE ROW LEVEL SECURITY;
ALTER TABLE contracts ENABLE ROW LEVEL SECURITY;
ALTER TABLE promissory_notes ENABLE ROW LEVEL SECURITY;
ALTER TABLE financial_transactions ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit_logs ENABLE ROW LEVEL SECURITY;

-- سياسات الوصول الشاملة للـ Web App عبر anon key المصرح به (مزامنة كاملة ثنائية الاتجاه قابلة للتشغيل المتكرر)
DROP POLICY IF EXISTS "Allow anon all company_profile" ON company_profile;
CREATE POLICY "Allow anon all company_profile" ON company_profile FOR ALL USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Allow anon all users" ON users;
CREATE POLICY "Allow anon all users" ON users FOR ALL USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Allow anon all customers" ON customers;
CREATE POLICY "Allow anon all customers" ON customers FOR ALL USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Allow anon all blacklist" ON blacklist_records;
CREATE POLICY "Allow anon all blacklist" ON blacklist_records FOR ALL USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Allow anon all vehicles" ON vehicles;
CREATE POLICY "Allow anon all vehicles" ON vehicles FOR ALL USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Allow anon all contracts" ON contracts;
CREATE POLICY "Allow anon all contracts" ON contracts FOR ALL USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Allow anon all promissory" ON promissory_notes;
CREATE POLICY "Allow anon all promissory" ON promissory_notes FOR ALL USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Allow anon all transactions" ON financial_transactions;
CREATE POLICY "Allow anon all transactions" ON financial_transactions FOR ALL USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Allow anon all audit" ON audit_logs;
CREATE POLICY "Allow anon all audit" ON audit_logs FOR ALL USING (true) WITH CHECK (true);

-- ===============================================================================
-- 14. البيانات الأساسية للبدء الفوري (Initial Seed Data)
-- ===============================================================================

-- ملف وهوية الشركة
INSERT INTO company_profile (
    company_name, company_english_name, commercial_register_number, primary_phone, address
) VALUES (
    'شركة تأجير السيارات الحديثة', 'Modern Car Rental Co.', '401928374', '0566655550', 'طوباس - فلسطين'
) ON CONFLICT DO NOTHING;

-- المستخدمين الأساسيين (بنفس رموز PIN المعتمدة بالسيستم المكتبي)
INSERT INTO users (username, full_name, pin_code, role, phone_number) VALUES
('admin', 'حسين (المدير العام)', '1234', 'Admin', '0566655550'),
('staff', 'موظف الاستقبال', '5678', 'Staff', '0598776026'),
('accountant', 'محاسب النظام', '0000', 'Accountant', '0599000000')
ON CONFLICT (username) DO NOTHING;

-- أسطول سيارات المكتب المعتمد الـ 12
INSERT INTO vehicles (plate_number, brand, model, model_year, color, current_odometer, daily_standard_rate, status, image_url) VALUES
('6-1021-95', 'هيونداي', 'أكسنت أحمر', 2023, 'أحمر', 25000, 150.0, 'Available', '/images/cars/accent_red.webp'),
('6-0639-95', 'هيونداي', 'أكسنت بصمة', 2023, 'فضي', 32000, 150.0, 'Available', '/images/cars/accent_smart.jpg'),
('61', 'هيونداي', 'أكسنت 61', 2022, 'رمادي فيراني', 41000, 140.0, 'Available', '/images/cars/accent_61.jpg'),
('6-1022-95', 'هيونداي', 'أكسنت أبيض', 2023, 'أبيض', 28000, 150.0, 'Rented', '/images/cars/accent_white.jpg'),
('6-2031-95', 'كيا', 'ستونيك (Kia Stonic)', 2023, 'أحمر', 21000, 180.0, 'Available', '/images/cars/kia_stonic.jpg'),
('6-2032-95', 'كيا', 'سبورتاج (Kia Sportage)', 2022, 'أبيض', 35000, 230.0, 'Available', '/images/cars/kia_sportage.jpg'),
('6-3041-95', 'هيونداي', 'فينيو رمادي (Hyundai Venue)', 2023, 'رمادي', 19500, 160.0, 'Available', '/images/cars/venue_grey.webp'),
('6-3042-95', 'هيونداي', 'فينيو كحلي (Hyundai Venue)', 2023, 'كحلي', 22000, 160.0, 'Available', '/images/cars/venue_navy.jpg'),
('6-3043-95', 'هيونداي', 'فينيو أسود (Hyundai Venue)', 2023, 'أسود', 18000, 160.0, 'Available', '/images/cars/venue_black.webp'),
('6-3044-95', 'هيونداي', 'فينيو خمري (Hyundai Venue)', 2023, 'خمري', 26000, 160.0, 'Rented', '/images/cars/venue_burgundy.jpg'),
('6-3045-95', 'هيونداي', 'فينيو أحمر (Hyundai Venue)', 2023, 'أحمر', 24000, 160.0, 'Available', '/images/cars/venue_red.webp'),
('6-4051-95', 'كيا', 'بيكانتو (Kia Picanto)', 2024, 'أبيض', 14000, 120.0, 'Available', '/images/cars/kia_picanto.png')
ON CONFLICT (plate_number) DO NOTHING;

-- عملاء مسجلين بالنظام (للتجربة الفورية للتأجير من الهاتف)
INSERT INTO customers (full_name, national_id, primary_phone, license_number, license_expiry_date, date_of_birth, address, total_debts) VALUES
('حسين احمد راشد دراغمه', '411993819', '0566655550', 'DL-411993', CURRENT_DATE + INTERVAL '2 years', '1990-05-15', 'طوباس', 0.0),
('زيد بشار فريح دراغمه', '409991781', '0598776026', 'DL-409991', CURRENT_DATE + INTERVAL '2 years', '1998-08-20', 'طوباس', 80.0),
('محمد لؤي محمد ابو دواس', '409996816', '0598733510', 'DL-409996', CURRENT_DATE + INTERVAL '3 years', '1995-11-12', 'طوباس', 0.0),
('احمد عنان حسني دراغمة', '40998632', '059976532', 'DL-409986', CURRENT_DATE + INTERVAL '1 year', '1994-03-10', 'طوباس', 0.0)
ON CONFLICT (national_id) DO NOTHING;

-- ===============================================================================
-- 15. تفعيل محرك المزامنة اللحظية الفائقة (Supabase Realtime Engine)
-- ===============================================================================
-- إضافة الجداول لمنشور البث المباشر في سوبابيز لضمان التحديث في أجزاء من الثانية
DO $$
BEGIN
    -- إضافة جدول السيارات
    IF NOT EXISTS (SELECT 1 FROM pg_publication_tables WHERE pubname = 'supabase_realtime' AND tablename = 'vehicles') THEN
        ALTER PUBLICATION supabase_realtime ADD TABLE vehicles;
    END IF;

    -- إضافة جدول العقود
    IF NOT EXISTS (SELECT 1 FROM pg_publication_tables WHERE pubname = 'supabase_realtime' AND tablename = 'contracts') THEN
        ALTER PUBLICATION supabase_realtime ADD TABLE contracts;
    END IF;

    -- إضافة جدول العملاء
    IF NOT EXISTS (SELECT 1 FROM pg_publication_tables WHERE pubname = 'supabase_realtime' AND tablename = 'customers') THEN
        ALTER PUBLICATION supabase_realtime ADD TABLE customers;
    END IF;

    -- إضافة جدول المستخدمين
    IF NOT EXISTS (SELECT 1 FROM pg_publication_tables WHERE pubname = 'supabase_realtime' AND tablename = 'users') THEN
        ALTER PUBLICATION supabase_realtime ADD TABLE users;
    END IF;

    -- إضافة جدول المعاملات المالية
    IF NOT EXISTS (SELECT 1 FROM pg_publication_tables WHERE pubname = 'supabase_realtime' AND tablename = 'financial_transactions') THEN
        ALTER PUBLICATION supabase_realtime ADD TABLE financial_transactions;
    END IF;
END $$;

-- تفعيل إرجاع كامل السجل عند التعديل والحذف للبث اللحظي الفوري
ALTER TABLE vehicles REPLICA IDENTITY FULL;
ALTER TABLE contracts REPLICA IDENTITY FULL;
ALTER TABLE customers REPLICA IDENTITY FULL;
ALTER TABLE users REPLICA IDENTITY FULL;
ALTER TABLE financial_transactions REPLICA IDENTITY FULL;

-- ===============================================================================
-- نهاية السكريبت - تم بنجاح!
-- ===============================================================================
