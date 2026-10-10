import React, { useState, useEffect } from 'react';
import { X, Calendar, Clock, ShieldAlert, CheckCircle2, Car } from 'lucide-react';
import { supabase } from '../supabaseClient';

export default function RentCarModal({
  isOpen,
  onClose,
  vehicles,
  customers,
  initialVehicleId,
  initialCustomerId,
  currentUser,
  onRentSuccess
}) {
  const [selectedVehicleId, setSelectedVehicleId] = useState(initialVehicleId || '');
  const [selectedCustomerId, setSelectedCustomerId] = useState(initialCustomerId || '');
  const [customerSearch, setCustomerSearch] = useState('');

  const [rentalDays, setRentalDays] = useState(1);
  const [isOpenEnded, setIsOpenEnded] = useState(false);
  const [startDateTime, setStartDateTime] = useState(() => {
    const now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
    return now.toISOString().slice(0, 16);
  });

  const [dailyRate, setDailyRate] = useState(150);
  const [totalAgreedAmount, setTotalAgreedAmount] = useState(150);
  const [paymentMode, setPaymentMode] = useState('ImmediateCash');
  const [paidAmount, setPaidAmount] = useState(150);
  const [isUserCustomPaid, setIsUserCustomPaid] = useState(false);
  const [promissoryAmount, setPromissoryAmount] = useState(10000);
  const [notes, setNotes] = useState('');

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState('');
  const [submitSuccess, setSubmitSuccess] = useState(null);

  useEffect(() => {
    if (isOpen) {
      if (initialVehicleId) setSelectedVehicleId(initialVehicleId);
      if (initialCustomerId) setSelectedCustomerId(initialCustomerId);
      setSubmitSuccess(null);
      setSubmitError('');
    }
  }, [isOpen, initialVehicleId, initialCustomerId]);

  // تحديث السعر اليومي عند اختيار سيارة
  useEffect(() => {
    if (selectedVehicleId) {
      const v = vehicles.find(item => item.id === selectedVehicleId);
      if (v) {
        const rate = Number(v.daily_standard_rate || v.dailyStandardRate || 150);
        setDailyRate(rate);
        if (!isOpenEnded) {
          const tot = rate * rentalDays;
          setTotalAgreedAmount(tot);
          // لا نعدل على المبلغ إذا كتب المستخدم رقمه الخاص يدوياً
          if (!isUserCustomPaid) {
            setPaidAmount(tot);
          }
        }
      }
    }
  }, [selectedVehicleId, vehicles]);

  // إعادة احتساب الإجمالي عند تغيير الأيام
  const handleDaysChange = (days) => {
    setRentalDays(days);
    if (!isOpenEnded) {
      const rateNum = dailyRate === '' ? 0 : Number(dailyRate);
      const tot = rateNum * days;
      setTotalAgreedAmount(tot);
      if (!isUserCustomPaid) {
        setPaidAmount(tot);
      }
    }
  };

  const handlePaymentModeChange = (mode) => {
    setPaymentMode(mode);
    if (mode === 'DeferredOnReturn') {
      setPaidAmount(0);
      setIsUserCustomPaid(true);
    } else if (!isUserCustomPaid) {
      setPaidAmount(totalAgreedAmount);
    }
  };

  // كتابة المقبوض بحرية كاملة ودون فرض أي قيمة على المستخدم
  const handlePaidAmountChange = (rawVal) => {
    setIsUserCustomPaid(true);
    setPaidAmount(rawVal);
  };

  // أزرار سريعة اختيارية للراحة التامة
  const setQuickPaid = (amt) => {
    setIsUserCustomPaid(true);
    setPaidAmount(amt);
  };

  const selectedCustomer = customers.find(c => c.id === selectedCustomerId);
  const selectedVehicle = vehicles.find(v => v.id === selectedVehicleId);

  // حساب موعد الإرجاع
  const calculateReturnDate = () => {
    const d = new Date(startDateTime);
    d.setDate(d.getDate() + Number(rentalDays));
    return d.toLocaleString('ar-EG', { dateStyle: 'full', timeStyle: 'short' });
  };

  // تصفية العملاء حسب البحث
  const filteredCustomers = customers.filter(c => {
    if (!customerSearch) return true;
    const q = customerSearch.toLowerCase();
    return (
      (c.full_name || '').toLowerCase().includes(q) ||
      (c.national_id || '').includes(q) ||
      (c.primary_phone || '').includes(q)
    );
  });

  // فحص أمان المستأجر
  const isLicenseExpired = selectedCustomer && new Date(selectedCustomer.license_expiry_date) < new Date();
  const hasDebts = selectedCustomer && Number(selectedCustomer.total_debts || 0) > 0;
  const isBlacklisted = selectedCustomer?.is_blacklisted || false;

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!selectedVehicleId) {
      setSubmitError('يرجى اختيار مركبة متاحة للتأجير!');
      return;
    }
    if (!selectedCustomerId) {
      setSubmitError('يرجى اختيار مستأجر مسجل مسبقاً بالنظام!');
      return;
    }
    if (isBlacklisted) {
      setSubmitError('🚫 تنبيه أمني قاطع: هذا العميل محظور ومدرج بالقائمة السوداء! يمنع تأجيره.');
      return;
    }

    setIsSubmitting(true);
    setSubmitError('');

    try {
      const actualPaid = paidAmount === '' ? 0 : Number(paidAmount) || 0;
      const actualDailyRate = dailyRate === '' ? 150 : Number(dailyRate) || 150;
      const remainingAmount = Math.max(0, totalAgreedAmount - actualPaid);
      const expectedReturn = new Date(new Date(startDateTime).getTime() + rentalDays * 24 * 60 * 60 * 1000).toISOString();

      if (supabase) {
        // محاولة استدعاء دالة التأجير الذرية المقفلة
        const { data, error } = await supabase.rpc('rent_vehicle_atomic', {
          p_customer_id: selectedCustomerId,
          p_vehicle_id: selectedVehicleId,
          p_rental_days: rentalDays,
          p_start_date: new Date(startDateTime).toISOString(),
          p_expected_return: expectedReturn,
          p_is_open_ended: isOpenEnded,
          p_daily_rate: actualDailyRate,
          p_total_amount: totalAgreedAmount,
          p_paid_amount: actualPaid,
          p_remaining_amount: remainingAmount,
          p_payment_mode: remainingAmount > 0 ? (actualPaid > 0 ? 'ImmediateCash' : 'DeferredOnReturn') : paymentMode,
          p_promissory_amount: promissoryAmount,
          p_staff_username: currentUser?.username || 'mobile_user',
          p_notes: notes || 'تأجير عبر تطبيق الهاتف الذكي'
        });

        if (error) {
          console.warn('RPC rent_vehicle_atomic error, falling back to direct table update:', error);
          const { error: vError } = await supabase
            .from('vehicles')
            .update({ status: 'Rented' })
            .eq('id', selectedVehicleId)
            .eq('status', 'Available');

          if (vError) throw new Error('المركبة لم تعد متاحة للتأجير!');

          const contractNo = `CRM-${new Date().getFullYear()}-${Math.floor(1000 + Math.random() * 9000)}`;
          await supabase.from('contracts').insert({
            contract_number: contractNo,
            customer_id: selectedCustomerId,
            vehicle_id: selectedVehicleId,
            start_date_time: new Date(startDateTime).toISOString(),
            expected_return_date_time: expectedReturn,
            is_open_ended: isOpenEnded,
            daily_agreed_rate: actualDailyRate,
            total_agreed_amount: totalAgreedAmount,
            paid_amount: actualPaid,
            remaining_amount: remainingAmount,
            payment_mode: remainingAmount > 0 ? (actualPaid > 0 ? 'ImmediateCash' : 'DeferredOnReturn') : paymentMode,
            status: 'Active',
            promissory_note_amount: promissoryAmount,
            start_odometer: selectedVehicle?.current_odometer || 0,
            created_by_staff: currentUser?.username || 'admin',
            notes: notes
          });

          if (remainingAmount > 0) {
            await supabase.from('customers')
              .update({ total_debts: Number(selectedCustomer?.total_debts || 0) + remainingAmount })
              .eq('id', selectedCustomerId);
          }
        } else if (data && !data.success) {
          throw new Error(data.message || 'فشلت عملية التأجير!');
        }
      }

      setSubmitSuccess({
        vehicle: selectedVehicle,
        customer: selectedCustomer,
        total: totalAgreedAmount,
        paid: actualPaid,
        remaining: remainingAmount
      });

      if (onRentSuccess) {
        onRentSuccess({
          vehicle_id: selectedVehicleId,
          customer_id: selectedCustomerId,
          total_agreed_amount: totalAgreedAmount,
          paid_amount: actualPaid,
          remaining_amount: remainingAmount,
          start_date_time: new Date(startDateTime).toISOString(),
          expected_return_date_time: expectedReturn,
          status: 'Active'
        });
      }

    } catch (err) {
      console.error('Rent error:', err);
      setSubmitError(err.message || 'حدث خطأ أثناء توثيق عقد التأجير!');
    } finally {
      setIsSubmitting(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={e => e.stopPropagation()}>
        {/* ترويسة النافذة الملكية */}
        <div className="modal-header">
          <div className="modal-title" style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Car size={20} color="var(--wood-amber)" />
            <span>فتح عقد وتأجير لعميل مسجل</span>
          </div>
          <button type="button" className="modal-close-btn" onClick={onClose} title="إغلاق">
            <X size={18} />
          </button>
        </div>

        <div className="modal-body">
          {submitSuccess ? (
            <div style={{ textAlign: 'center', padding: '20px 8px' }}>
              <CheckCircle2 size={56} color="var(--accent-green)" style={{ margin: '0 auto 12px' }} />
              <h2 style={{ fontSize: '1.25rem', fontWeight: 900, color: 'var(--accent-green)' }}>
                تم توثيق وتأجير المركبة بنجاح!
              </h2>
              <p style={{ fontSize: '0.84rem', color: 'var(--text-muted)', marginTop: '4px' }}>
                تم تحويل حالة السيارة إلى (مؤجرة 🔴) وتوثيق العقد وقيد المبالغ فورياً في النظام.
              </p>

              <div style={{
                background: '#fdfaf4',
                border: '1.5px solid var(--border-color)',
                borderRadius: '12px',
                padding: '14px',
                margin: '18px 0',
                textAlign: 'right',
                fontSize: '0.86rem',
                display: 'flex',
                flexDirection: 'column',
                gap: '8px'
              }}>
                <div>🚗 <strong>المركبة:</strong> {submitSuccess.vehicle?.brand} {submitSuccess.vehicle?.model} (لوحة: <bdi dir="ltr" style={{ fontWeight: 900 }}>{submitSuccess.vehicle?.plate_number}</bdi>)</div>
                <div>👤 <strong>المستأجر:</strong> {submitSuccess.customer?.full_name}</div>
                <div>💰 <strong>المبلغ الإجمالي:</strong> {submitSuccess.total} ₪ (المدفوع: {submitSuccess.paid} ₪ | المتبقي: {submitSuccess.remaining} ₪)</div>
              </div>

              <button
                type="button"
                className="btn-primary-action"
                style={{ background: 'linear-gradient(135deg, #10b981 0%, #059669 100%)', borderColor: '#059669' }}
                onClick={onClose}
              >
                ✓ تم والعودة للأسطول
              </button>
            </div>
          ) : (
            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              {submitError && (
                <div className="alert-banner alert-danger">
                  <ShieldAlert size={20} />
                  <span>{submitError}</span>
                </div>
              )}

              {/* 1. اختيار السيارة */}
              <div>
                <label className="form-label">1. حدد المركبة (المتاحة فقط):</label>
                <select
                  className="form-select"
                  value={selectedVehicleId}
                  onChange={e => setSelectedVehicleId(e.target.value)}
                  required
                >
                  <option value="">-- اختر سيارة من الأسطول --</option>
                  {vehicles.filter(v => v.status === 'Available').map(v => (
                    <option key={v.id} value={v.id}>
                      🟢 {v.brand} {v.model} ({v.model_year}) - لوحة: [{'\u2066' + v.plate_number + '\u2069'}] ({v.daily_standard_rate} ₪/يوم)
                    </option>
                  ))}
                </select>
              </div>

              {/* 2. اختيار المستأجر من المسجلين */}
              <div>
                <label className="form-label">2. حدد المستأجر (مسجل مسبقاً بالنظام):</label>
                <input
                  type="text"
                  className="form-input"
                  placeholder="🔍 بحث سريع باسم العميل أو رقم الهوية..."
                  value={customerSearch}
                  onChange={e => setCustomerSearch(e.target.value)}
                  style={{ marginBottom: '8px', padding: '8px 12px', fontSize: '0.85rem' }}
                />
                <select
                  className="form-select"
                  value={selectedCustomerId}
                  onChange={e => setSelectedCustomerId(e.target.value)}
                  required
                >
                  <option value="">-- اختر المستأجر ({filteredCustomers.length} عميل) --</option>
                  {filteredCustomers.map(c => (
                    <option key={c.id} value={c.id}>
                      {c.full_name} (هوية: {c.national_id} | هاتف: {c.primary_phone})
                    </option>
                  ))}
                </select>

                {/* تنبيهات أمان المستأجر الفورية */}
                {selectedCustomer && (
                  <div style={{ marginTop: '8px', display: 'flex', flexDirection: 'column', gap: '6px' }}>
                    {isBlacklisted && (
                      <div className="alert-banner alert-danger">
                        🚫 العميل مدرج في القائمة السوداء! يمنع فتح العقد تماماً.
                      </div>
                    )}
                    {isLicenseExpired && (
                      <div className="alert-banner alert-warning">
                        ⚠️ رخصة القيادة للعميل منتهية الصلاحية!
                      </div>
                    )}
                    {hasDebts && (
                      <div className="alert-banner alert-warning">
                        💳 تنبيه مالي: على العميل ذمم سابقة مستحقة بقيمة ({selectedCustomer.total_debts} ₪).
                      </div>
                    )}
                  </div>
                )}
              </div>

              {/* 3. مدة التأجير والتاريخ */}
              <div style={{
                background: '#fcfaf6',
                padding: '12px 14px',
                borderRadius: '12px',
                border: '1.5px solid var(--border-color)'
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                  <label className="form-label" style={{ margin: 0 }}>مدة الإيجار بالأيام:</label>
                  <span style={{ fontSize: '0.88rem', fontWeight: 900, color: 'var(--primary-blue)' }}>
                    {rentalDays} يوم
                  </span>
                </div>

                {/* شرائح الأيام السريعة */}
                <div style={{ display: 'flex', gap: '6px', overflowX: 'auto', paddingBottom: '4px' }}>
                  {[1, 2, 3, 5, 7, 10, 14, 30].map(d => {
                    const isSel = rentalDays === d;
                    return (
                      <button
                        key={d}
                        type="button"
                        onClick={() => handleDaysChange(d)}
                        style={{
                          padding: '6px 14px',
                          borderRadius: '20px',
                          border: isSel ? '1.5px solid var(--wood-amber)' : '1px solid var(--border-color)',
                          background: isSel ? 'var(--wood-btn-gradient)' : '#ffffff',
                          color: isSel ? '#ffffff' : 'var(--text-dark)',
                          fontSize: '0.8rem',
                          fontWeight: 800,
                          cursor: 'pointer',
                          whiteSpace: 'nowrap',
                          boxShadow: isSel ? '0 2px 6px rgba(45, 24, 12, 0.2)' : 'none',
                          transition: 'all 0.15s ease'
                        }}
                      >
                        {d === 7 ? 'أسبوع' : d === 14 ? 'أسبوعين' : d === 30 ? 'شهر' : `${d} يوم`}
                      </button>
                    );
                  })}
                </div>

                <div style={{ marginTop: '10px', fontSize: '0.78rem', color: 'var(--accent-green)', fontWeight: 800 }}>
                  ⏱️ موعد الإرجاع المتوقع: {calculateReturnDate()}
                </div>
              </div>

              {/* 4. الحسابات المالية وطريقة الدفع */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                <div>
                  <label className="form-label">السعر اليومي (₪):</label>
                  <input
                    type="text"
                    inputMode="numeric"
                    className="form-input"
                    value={dailyRate}
                    onFocus={e => {
                      if (dailyRate === 0 || dailyRate === '0') setDailyRate('');
                    }}
                    onChange={e => {
                      const val = e.target.value.replace(/[^0-9]/g, '');
                      setDailyRate(val);
                      const r = val === '' ? 0 : Number(val);
                      const tot = r * rentalDays;
                      setTotalAgreedAmount(tot);
                      if (!isUserCustomPaid) setPaidAmount(tot);
                    }}
                    onBlur={() => {
                      if (dailyRate === '') setDailyRate(150);
                    }}
                  />
                </div>
                <div>
                  <label className="form-label">إجمالي العقد (₪):</label>
                  <input
                    type="text"
                    className="form-input"
                    value={`${totalAgreedAmount} ₪`}
                    readOnly
                    style={{ fontWeight: 900, color: 'var(--wood-amber)', background: '#fdfaf4' }}
                  />
                </div>
              </div>

              <div>
                <label className="form-label">طريقة الدفع:</label>
                <select
                  className="form-select"
                  value={paymentMode}
                  onChange={e => handlePaymentModeChange(e.target.value)}
                >
                  <option value="ImmediateCash">دفع فوري نقداً عند الاستلام</option>
                  <option value="DeferredOnReturn">دفع آجل عند التسليم (ذمم كاملة)</option>
                  <option value="BankTransfer">تحويل بنكي</option>
                  <option value="CreditCard">بطاقة مدى / ائتمان</option>
                </select>
              </div>

              {/* المقبوض والمتبقي كدين - حرية كتابة تامة دون أصفار عالقة ودون إجبار */}
              <div style={{
                background: '#fcfaf6',
                border: '1.5px solid var(--border-color)',
                borderRadius: '12px',
                padding: '12px 14px',
                display: 'flex',
                flexDirection: 'column',
                gap: '10px'
              }}>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                  <div>
                    <label className="form-label" style={{ fontWeight: 800 }}>المقبوض الآن (₪):</label>
                    <input
                      type="text"
                      inputMode="numeric"
                      className="form-input"
                      placeholder="اكتب المبلغ المقبوض..."
                      value={paidAmount}
                      onFocus={e => {
                        if (paidAmount === 0 || paidAmount === '0') handlePaidAmountChange('');
                      }}
                      onChange={e => {
                        const val = e.target.value.replace(/[^0-9]/g, '');
                        handlePaidAmountChange(val);
                      }}
                      style={{ fontWeight: 900, fontSize: '1rem', color: 'var(--wood-walnut)' }}
                    />
                  </div>
                  <div>
                    {(() => {
                      const tot = Number(totalAgreedAmount) || 0;
                      const actualPaid = paidAmount === '' ? 0 : Number(paidAmount) || 0;
                      const rem = Math.max(0, tot - actualPaid);
                      return (
                        <div>
                          <label className="form-label" style={{ fontWeight: 800 }}>المتبقي كدين (₪):</label>
                          <input
                            type="text"
                            className="form-input"
                            value={`${rem} ₪`}
                            readOnly
                            style={{
                              color: rem > 0 ? '#dc2626' : '#16a34a',
                              fontWeight: 900,
                              fontSize: '1rem',
                              background: rem > 0 ? '#fef2f2' : '#f0fdf4',
                              border: rem > 0 ? '1.5px solid #fca5a5' : '1.5px solid #86efac'
                            }}
                          />
                        </div>
                      );
                    })()}
                  </div>
                </div>

                {/* أزرار سريعة ومريحة لملء المقبوض بنقرة واحدة إذا رغب المستخدم */}
                <div style={{ display: 'flex', gap: '6px', alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: '0.72rem', color: 'var(--text-muted)', fontWeight: 700 }}>خيارات سريعة:</span>
                  <button
                    type="button"
                    onClick={() => setQuickPaid(totalAgreedAmount)}
                    style={{
                      fontSize: '0.74rem',
                      padding: '3px 10px',
                      borderRadius: '14px',
                      border: Number(paidAmount) === totalAgreedAmount ? '1.5px solid #10b981' : '1px solid var(--border-color)',
                      background: Number(paidAmount) === totalAgreedAmount ? '#ecfdf5' : '#ffffff',
                      color: Number(paidAmount) === totalAgreedAmount ? '#047857' : 'var(--text-muted)',
                      fontWeight: 800,
                      cursor: 'pointer'
                    }}
                  >
                    دفع كامل ({totalAgreedAmount} ₪)
                  </button>
                  <button
                    type="button"
                    onClick={() => setQuickPaid(Math.round(totalAgreedAmount / 2))}
                    style={{
                      fontSize: '0.74rem',
                      padding: '3px 10px',
                      borderRadius: '14px',
                      border: Number(paidAmount) === Math.round(totalAgreedAmount / 2) ? '1.5px solid #3b82f6' : '1px solid var(--border-color)',
                      background: Number(paidAmount) === Math.round(totalAgreedAmount / 2) ? '#eff6ff' : '#ffffff',
                      color: Number(paidAmount) === Math.round(totalAgreedAmount / 2) ? '#1d4ed8' : 'var(--text-muted)',
                      fontWeight: 800,
                      cursor: 'pointer'
                    }}
                  >
                    دفع النصف ({Math.round(totalAgreedAmount / 2)} ₪)
                  </button>
                  <button
                    type="button"
                    onClick={() => setQuickPaid(0)}
                    style={{
                      fontSize: '0.74rem',
                      padding: '3px 10px',
                      borderRadius: '14px',
                      border: (paidAmount === 0 || paidAmount === '0') ? '1.5px solid #ef4444' : '1px solid var(--border-color)',
                      background: (paidAmount === 0 || paidAmount === '0') ? '#fef2f2' : '#ffffff',
                      color: (paidAmount === 0 || paidAmount === '0') ? '#b91c1c' : 'var(--text-muted)',
                      fontWeight: 800,
                      cursor: 'pointer'
                    }}
                  >
                    دين كامل (0 ₪)
                  </button>
                </div>

                {/* شريط توضيح تصنيف الدين التلقائي */}
                {(() => {
                  const tot = Number(totalAgreedAmount) || 0;
                  const actualPaid = paidAmount === '' ? 0 : Number(paidAmount) || 0;
                  const rem = Math.max(0, tot - actualPaid);
                  if (rem > 0) {
                    return (
                      <div style={{
                        background: '#fff1f2',
                        border: '1px solid #fecdd3',
                        borderRadius: '8px',
                        padding: '8px 12px',
                        fontSize: '0.78rem',
                        color: '#9f1239',
                        fontWeight: 800,
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px'
                      }}>
                        <span>💳</span>
                        <span>
                          سيتم تسجيل مبلغ <strong>({rem} ₪)</strong> كدين وذمة مستحقة على العميل {selectedCustomer ? `(${selectedCustomer.full_name})` : ''} في كشف حسابه تلقائياً.
                        </span>
                      </div>
                    );
                  } else {
                    return (
                      <div style={{
                        background: '#f0fdf4',
                        border: '1px solid #bbf7d0',
                        borderRadius: '8px',
                        padding: '8px 12px',
                        fontSize: '0.78rem',
                        color: '#166534',
                        fontWeight: 800,
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px'
                      }}>
                        <span>✓</span>
                        <span>العقد مسدد بالكامل نقداً (لا توجد أي ذمم أو ديون متبقية على العميل).</span>
                      </div>
                    );
                  }
                })()}
              </div>

              <button
                type="submit"
                disabled={isSubmitting || isBlacklisted}
                className="btn-primary-action"
                style={{ marginTop: '8px', padding: '14px', fontSize: '1rem' }}
              >
                {isSubmitting ? 'جاري توثيق الحجز والعقد...' : '✓ تأكيد التأجير وإصدار العقد الآن'}
              </button>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}
