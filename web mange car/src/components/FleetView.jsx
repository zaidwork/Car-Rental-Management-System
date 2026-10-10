import React, { useState } from 'react';
import { Search, Phone, MessageSquare, Calendar, Clock, AlertTriangle, Key, ShieldCheck, User, Car } from 'lucide-react';

export default function FleetView({ vehicles, contracts, customers, onRentCarClick }) {
  const [filter, setFilter] = useState('ALL'); // ALL, Available, Rented, InMaintenance
  const [searchQuery, setSearchQuery] = useState('');

  // إحصائيات الأسطول
  const totalVehicles = vehicles.length;
  const availableCount = vehicles.filter(v => v.status === 'Available').length;
  const rentedCount = vehicles.filter(v => v.status === 'Rented').length;
  const maintCount = vehicles.filter(v => v.status === 'InMaintenance').length;

  // تصفية المركبات
  const filteredVehicles = vehicles.filter(v => {
    if (filter !== 'ALL' && v.status !== filter) return false;
    if (searchQuery) {
      const q = searchQuery.toLowerCase();
      const matchPlate = (v.plate_number || '').toLowerCase().includes(q);
      const matchBrand = (v.brand || '').toLowerCase().includes(q);
      const matchModel = (v.model || '').toLowerCase().includes(q);
      return matchPlate || matchBrand || matchModel;
    }
    return true;
  });

  // العقد النشط للسيارة المؤجرة
  const getActiveContractForVehicle = (vehicleId) => {
    return contracts.find(c => (c.vehicle_id === vehicleId || c.vehicleId === vehicleId) && c.status === 'Active');
  };

  // العميل المرتبط بالعقد
  const getCustomerForContract = (customerId) => {
    return customers.find(cu => cu.id === customerId);
  };

  // استخراج مسار الصورة مع دعم Base64 والمسارات النسبية
  const getCarImageSrc = (url) => {
    if (!url || typeof url !== 'string' || !url.trim()) return '/images/cars/accent_white.jpg';
    const trimmed = url.trim();
    if (trimmed.startsWith('data:image') || trimmed.startsWith('http://') || trimmed.startsWith('https://')) return trimmed;
    if (trimmed.startsWith('/')) return trimmed;
    return '/' + trimmed;
  };

  // حساب موعد التسليم والمتبقي أو التأخير
  const getRemainingTimeInfo = (expectedReturnStr) => {
    if (!expectedReturnStr) return null;
    const now = new Date();
    const returnDate = new Date(expectedReturnStr);
    const diffMs = returnDate - now;

    if (diffMs < 0) {
      const lateHours = Math.abs(Math.floor(diffMs / (1000 * 60 * 60)));
      return {
        isLate: true,
        text: `🚨 متأخرة عن التسليم منذ ${lateHours >= 24 ? Math.floor(lateHours / 24) + ' يوم و ' + (lateHours % 24) + ' س' : lateHours + ' ساعة'}`
      };
    } else {
      const remHours = Math.floor(diffMs / (1000 * 60 * 60));
      return {
        isLate: false,
        text: `⏳ متبقي للتسليم: ${remHours >= 24 ? Math.floor(remHours / 24) + ' يوم و ' + (remHours % 24) + ' س' : remHours + ' ساعة'}`
      };
    }
  };

  return (
    <div style={{ paddingBottom: '20px' }}>
      {/* 1. مؤشرات الأسطول التنفيذية (2x2 Grid متوازن ومريح للعين على شاشات الهاتف) */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: '10px',
        padding: '14px 16px 8px 16px'
      }}>
        <div style={{
          background: '#ffffff',
          border: '1.5px solid var(--border-color)',
          borderRadius: '14px',
          padding: '12px 14px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          boxShadow: '0 2px 6px rgba(45, 24, 12, 0.04)'
        }}>
          <div>
            <div style={{ fontSize: '0.74rem', fontWeight: 800, color: 'var(--text-muted)' }}>أسطول المكتب</div>
            <div style={{ fontSize: '1.45rem', fontWeight: 900, color: 'var(--wood-walnut)', lineHeight: 1.1, marginTop: '2px' }}>
              {totalVehicles} <span style={{ fontSize: '0.8rem', fontWeight: 700 }}>مركبة</span>
            </div>
          </div>
          <div style={{ width: '38px', height: '38px', borderRadius: '10px', background: '#f5f0e6', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Car size={20} color="var(--wood-amber)" />
          </div>
        </div>

        <div style={{
          background: 'linear-gradient(180deg, #ffffff 0%, #f0fdf4 100%)',
          border: '1.5px solid #86efac',
          borderRadius: '14px',
          padding: '12px 14px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          boxShadow: '0 2px 6px rgba(5, 150, 105, 0.06)'
        }}>
          <div>
            <div style={{ fontSize: '0.74rem', fontWeight: 800, color: '#166534' }}>متاحة للتأجير</div>
            <div style={{ fontSize: '1.45rem', fontWeight: 900, color: 'var(--accent-green)', lineHeight: 1.1, marginTop: '2px' }}>
              {availableCount} <span style={{ fontSize: '0.8rem', fontWeight: 700 }}>سيارة</span>
            </div>
          </div>
          <div style={{ width: '38px', height: '38px', borderRadius: '10px', background: '#dcfce7', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <span style={{ fontSize: '1.1rem' }}>🟢</span>
          </div>
        </div>

        <div style={{
          background: 'linear-gradient(180deg, #ffffff 0%, #fef2f2 100%)',
          border: '1.5px solid #fca5a5',
          borderRadius: '14px',
          padding: '12px 14px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          boxShadow: '0 2px 6px rgba(220, 38, 38, 0.06)'
        }}>
          <div>
            <div style={{ fontSize: '0.74rem', fontWeight: 800, color: '#991b1b' }}>مؤجرة حالياً</div>
            <div style={{ fontSize: '1.45rem', fontWeight: 900, color: 'var(--accent-red)', lineHeight: 1.1, marginTop: '2px' }}>
              {rentedCount} <span style={{ fontSize: '0.8rem', fontWeight: 700 }}>سيارة</span>
            </div>
          </div>
          <div style={{ width: '38px', height: '38px', borderRadius: '10px', background: '#fee2e2', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <span style={{ fontSize: '1.1rem' }}>🔴</span>
          </div>
        </div>

        <div style={{
          background: 'linear-gradient(180deg, #ffffff 0%, #fffbeb 100%)',
          border: '1.5px solid #fcd34d',
          borderRadius: '14px',
          padding: '12px 14px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          boxShadow: '0 2px 6px rgba(217, 119, 6, 0.06)'
        }}>
          <div>
            <div style={{ fontSize: '0.74rem', fontWeight: 800, color: '#92400e' }}>بالصيانة</div>
            <div style={{ fontSize: '1.45rem', fontWeight: 900, color: 'var(--accent-orange)', lineHeight: 1.1, marginTop: '2px' }}>
              {maintCount} <span style={{ fontSize: '0.8rem', fontWeight: 700 }}>سيارة</span>
            </div>
          </div>
          <div style={{ width: '38px', height: '38px', borderRadius: '10px', background: '#fef3c7', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <span style={{ fontSize: '1.1rem' }}>⚠️</span>
          </div>
        </div>
      </div>

      {/* 2. شريط البحث الملكي (Search Box) */}
      <div style={{ padding: '0 16px', marginTop: '4px' }}>
        <div style={{
          display: 'flex',
          alignItems: 'center',
          background: '#ffffff',
          border: '1.5px solid var(--border-color)',
          borderRadius: '12px',
          padding: '10px 14px',
          gap: '10px',
          boxShadow: '0 2px 6px rgba(45, 24, 12, 0.04)'
        }}>
          <Search size={18} color="var(--wood-amber)" />
          <input
            type="text"
            placeholder="بحث برقم اللوحة أو اسم وموديل السيارة..."
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            style={{
              border: 'none',
              outline: 'none',
              background: 'transparent',
              width: '100%',
              fontFamily: 'inherit',
              fontSize: '0.9rem',
              fontWeight: 600,
              color: 'var(--text-main)'
            }}
          />
        </div>
      </div>

      {/* 3. شريط تصفية الأسطول (Scrollable Action Filter Strip) */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        overflowX: 'auto',
        padding: '10px 16px',
        scrollbarWidth: 'none',
        msOverflowStyle: 'none'
      }}>
        <button
          className={`filter-pill ${filter === 'ALL' ? 'active' : ''}`}
          onClick={() => setFilter('ALL')}
          style={{ whiteSpace: 'nowrap', padding: '8px 16px', fontSize: '0.84rem' }}
        >
          كل الأسطول ({totalVehicles})
        </button>

        <button
          className={`filter-pill ${filter === 'Available' ? 'active' : ''}`}
          onClick={() => setFilter('Available')}
          style={{ whiteSpace: 'nowrap', padding: '8px 16px', fontSize: '0.84rem' }}
        >
          🟢 المتاحة ({availableCount})
        </button>

        <button
          className={`filter-pill ${filter === 'Rented' ? 'active' : ''}`}
          onClick={() => setFilter('Rented')}
          style={{ whiteSpace: 'nowrap', padding: '8px 16px', fontSize: '0.84rem' }}
        >
          🔴 المؤجرة ({rentedCount})
        </button>

        <button
          className={`filter-pill ${filter === 'InMaintenance' ? 'active' : ''}`}
          onClick={() => setFilter('InMaintenance')}
          style={{ whiteSpace: 'nowrap', padding: '8px 16px', fontSize: '0.84rem' }}
        >
          ⚠️ الصيانة ({maintCount})
        </button>
      </div>

      {/* 4. بطاقات السيارات الملكية (مطابقة لسيستم Dashboard.razor) */}
      <div style={{ padding: '0 16px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
        {filteredVehicles.length === 0 ? (
          <div style={{
            textAlign: 'center',
            padding: '40px 20px',
            background: '#ffffff',
            borderRadius: '16px',
            border: '1.5px solid var(--border-color)',
            color: 'var(--text-muted)',
            fontWeight: 700
          }}>
            <Car size={42} color="#c8b9a1" style={{ margin: '0 auto 10px' }} />
            <p style={{ margin: 0 }}>لا توجد مركبات تطابق معايير البحث الحالية.</p>
          </div>
        ) : (
          filteredVehicles.map(v => {
            const isRented = v.status === 'Rented';
            const isAvailable = v.status === 'Available';
            const activeContract = isRented ? getActiveContractForVehicle(v.id) : null;
            const tenant = activeContract ? getCustomerForContract(activeContract.customer_id) : null;
            const timeInfo = activeContract ? getRemainingTimeInfo(activeContract.expected_return_date_time) : null;

            return (
              <div
                key={v.id}
                style={{
                  background: isRented ? '#fffafa' : '#ffffff',
                  border: isRented ? '2px solid #ef4444' : '1.5px solid var(--border-color)',
                  borderRadius: '18px',
                  padding: '16px',
                  boxShadow: isRented
                    ? '0 6px 20px rgba(239, 68, 68, 0.15)'
                    : '0 4px 16px rgba(45, 24, 12, 0.06)',
                  position: 'relative',
                  overflow: 'hidden'
                }}
              >
                {/* 1. إطار صورة السيارة واللوحة والسعر */}
                <div style={{
                  position: 'relative',
                  width: '100%',
                  height: '145px',
                  background: '#f8f5ee',
                  borderRadius: '12px',
                  border: '1px solid var(--border-subtle)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  overflow: 'hidden',
                  marginBottom: '12px'
                }}>
                  {/* شارة لوحة السيارة بنظام لوحة الترخيص الفلسطينية الموحدة (LTR صارم لمنع انقلاب الأرقام إطلاقاً) */}
                  <div style={{
                    position: 'absolute',
                    top: '8px',
                    right: '8px',
                    background: '#fef08a',
                    border: '1.5px solid #1c1917',
                    borderRadius: '5px',
                    padding: '2px 6px 2px 4px',
                    boxShadow: '0 2px 5px rgba(0,0,0,0.18)',
                    zIndex: 2,
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '5px',
                    direction: 'ltr',
                    unicodeBidi: 'isolate'
                  }}>
                    <span style={{
                      background: '#15803d',
                      color: '#ffffff',
                      fontSize: '0.62rem',
                      fontWeight: 900,
                      padding: '1px 4px',
                      borderRadius: '3px',
                      lineHeight: 1.1,
                      letterSpacing: '0.5px'
                    }}>
                      P
                    </span>
                    <span style={{
                      fontFamily: "'Consolas', 'Courier New', monospace",
                      fontWeight: 900,
                      fontSize: '0.88rem',
                      color: '#09090b',
                      letterSpacing: '0.8px',
                      lineHeight: 1.2,
                      direction: 'ltr',
                      unicodeBidi: 'isolate'
                    }}>
                      {v.plate_number}
                    </span>
                  </div>

                  {/* شارة السعر اليومي (Top Left) */}
                  <div style={{
                    position: 'absolute',
                    top: '8px',
                    left: '8px',
                    background: 'rgba(255, 255, 255, 0.95)',
                    border: '1px solid var(--border-color)',
                    color: 'var(--wood-walnut)',
                    padding: '3px 10px',
                    borderRadius: '6px',
                    fontWeight: 900,
                    fontSize: '0.82rem',
                    boxShadow: '0 2px 6px rgba(0,0,0,0.06)',
                    zIndex: 2
                  }}>
                    {v.daily_standard_rate || 150} ₪/يوم
                  </div>

                  {/* صورة السيارة الحقيقية */}
                  <img
                    src={getCarImageSrc(v.image_url)}
                    alt={`${v.brand} ${v.model}`}
                    style={{
                      maxWidth: '92%',
                      maxHeight: '92%',
                      objectFit: 'contain'
                    }}
                    onError={(e) => {
                      if (!e.target.src.endsWith('/images/cars/accent_white.jpg')) {
                        e.target.src = '/images/cars/accent_white.jpg';
                      }
                    }}
                  />
                </div>

                {/* 2. بيانات السيارة ومواصفاتها - رقعة الحالة لا تُقص أبداً سواء خضراء أو حمراء */}
                <div style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'flex-start',
                  gap: '10px',
                  marginBottom: '10px'
                }}>
                  <div style={{ flex: '1 1 auto', minWidth: 0 }}>
                    <h3 style={{
                      fontSize: '1.12rem',
                      fontWeight: 900,
                      color: 'var(--wood-walnut)',
                      margin: 0,
                      lineHeight: 1.35,
                      wordBreak: 'break-word',
                      overflowWrap: 'anywhere'
                    }}>
                      {v.brand} {v.model}
                    </h3>
                    <div style={{
                      fontSize: '0.78rem',
                      color: 'var(--text-muted)',
                      fontWeight: 700,
                      marginTop: '4px',
                      display: 'flex',
                      flexWrap: 'wrap',
                      alignItems: 'center',
                      gap: '6px'
                    }}>
                      <span>موديل {v.model_year}</span>
                      <span>•</span>
                      <span>{v.color}</span>
                      <span>•</span>
                      <span>عداد: {Number(v.current_odometer || 0).toLocaleString()} كم</span>
                    </div>
                  </div>

                  {/* مؤشر الحالة - رقعة بارزة وثابتة مستحيل أن تُقص إطلاقاً (خضراء أو حمراء) */}
                  <div style={{ flexShrink: 0, whiteSpace: 'nowrap' }}>
                    {isAvailable ? (
                      <span style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '5px',
                        background: '#ecfdf5',
                        color: '#065f46',
                        border: '1.5px solid #10b981',
                        padding: '5px 12px',
                        borderRadius: '10px',
                        fontWeight: 900,
                        fontSize: '0.8rem',
                        whiteSpace: 'nowrap',
                        boxShadow: '0 2px 5px rgba(16, 185, 129, 0.15)'
                      }}>
                        <span style={{ width: '8px', height: '8px', borderRadius: '50%', background: '#10b981', display: 'inline-block' }}></span>
                        <span>متاحة</span>
                      </span>
                    ) : isRented ? (
                      <span style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '5px',
                        background: '#fef2f2',
                        color: '#991b1b',
                        border: '1.5px solid #ef4444',
                        padding: '5px 12px',
                        borderRadius: '10px',
                        fontWeight: 900,
                        fontSize: '0.8rem',
                        whiteSpace: 'nowrap',
                        boxShadow: '0 2px 6px rgba(239, 68, 68, 0.2)'
                      }}>
                        <span style={{ width: '8px', height: '8px', borderRadius: '50%', background: '#ef4444', display: 'inline-block' }}></span>
                        <span>مؤجرة</span>
                      </span>
                    ) : (
                      <span style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '5px',
                        background: '#fffbeb',
                        color: '#92400e',
                        border: '1.5px solid #f59e0b',
                        padding: '5px 12px',
                        borderRadius: '10px',
                        fontWeight: 900,
                        fontSize: '0.8rem',
                        whiteSpace: 'nowrap'
                      }}>
                        <span style={{ width: '8px', height: '8px', borderRadius: '50%', background: '#f59e0b', display: 'inline-block' }}></span>
                        <span>صيانة</span>
                      </span>
                    )}
                  </div>
                </div>

                {/* 3. تفاصيل المستأجر الكاملة في حال كانت السيارة مؤجرة (حسب طلب المستخدم الدقيق) */}
                {isRented && (
                  <div style={{
                    background: 'linear-gradient(180deg, #ffffff 0%, #fdf8f7 100%)',
                    border: '1.5px solid #fecaca',
                    borderRadius: '12px',
                    padding: '12px 14px',
                    marginTop: '10px',
                    boxShadow: 'inset 0 1px 0 #ffffff'
                  }}>
                    {/* اسم المستأجر وهوية */}
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                        <User size={16} color="#dc2626" />
                        <strong style={{ fontSize: '0.94rem', color: '#1e293b' }}>
                          {tenant?.full_name || 'مستأجر مسجل بالنظام'}
                        </strong>
                      </div>
                      <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)', fontWeight: 700 }}>
                        هوية: {tenant?.national_id || '---'}
                      </span>
                    </div>

                    {/* مواعيد الخروج والتسليم */}
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px', marginBottom: '8px', fontSize: '0.76rem' }}>
                      <div style={{ background: '#ffffff', border: '1px solid #fee2e2', borderRadius: '8px', padding: '6px 8px' }}>
                        <div style={{ color: 'var(--text-muted)', fontWeight: 700 }}>📅 تاريخ وساعة الخروج:</div>
                        <div style={{ fontWeight: 800, color: 'var(--wood-walnut)', marginTop: '2px' }}>
                          {activeContract?.start_date_time
                            ? new Date(activeContract.start_date_time).toLocaleDateString('ar-EG', { month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit' })
                            : 'منذ يومين'}
                        </div>
                      </div>

                      <div style={{ background: '#ffffff', border: '1px solid #fee2e2', borderRadius: '8px', padding: '6px 8px' }}>
                        <div style={{ color: 'var(--text-muted)', fontWeight: 700 }}>⏱️ موعد التسليم المتوقع:</div>
                        <div style={{ fontWeight: 800, color: timeInfo?.isLate ? '#dc2626' : 'var(--wood-walnut)', marginTop: '2px' }}>
                          {activeContract?.expected_return_date_time
                            ? new Date(activeContract.expected_return_date_time).toLocaleDateString('ar-EG', { month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit' })
                            : 'اليوم'}
                        </div>
                      </div>
                    </div>

                    {/* تنبيه حالة المتبقي أو التأخير */}
                    {timeInfo && (
                      <div style={{
                        background: timeInfo.isLate ? '#fee2e2' : '#f0fdf4',
                        color: timeInfo.isLate ? '#991b1b' : '#166534',
                        border: `1.5px solid ${timeInfo.isLate ? '#ef4444' : '#86efac'}`,
                        borderRadius: '10px',
                        padding: '8px 12px',
                        fontSize: '0.78rem',
                        fontWeight: 800,
                        display: 'flex',
                        flexWrap: 'wrap',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        gap: '6px',
                        marginBottom: '10px'
                      }}>
                        <span style={{ flex: '1 1 auto', minWidth: '140px' }}>{timeInfo.text}</span>
                        {activeContract?.remaining_amount > 0 && (
                          <span style={{
                            color: '#991b1b',
                            background: '#ffffff',
                            border: '1px solid #fca5a5',
                            padding: '3px 8px',
                            borderRadius: '6px',
                            fontWeight: 900,
                            whiteSpace: 'nowrap'
                          }}>
                            متبقي بذمته: {activeContract.remaining_amount} ₪
                          </span>
                        )}
                      </div>
                    )}

                    {/* أزرار التواصل المباشر السريعة (واتساب + اتصال) */}
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                      <a
                        href={`https://wa.me/972${(tenant?.primary_phone || '').replace(/^0/, '')}`}
                        target="_blank"
                        rel="noreferrer"
                        className="btn-whatsapp-action"
                        style={{ padding: '8px 10px', fontSize: '0.8rem' }}
                      >
                        <MessageSquare size={15} />
                        <span>محادثة واتساب</span>
                      </a>

                      <a
                        href={`tel:${tenant?.primary_phone || ''}`}
                        className="btn-call-action"
                        style={{ padding: '8px 10px', fontSize: '0.8rem' }}
                      >
                        <Phone size={15} />
                        <span>اتصال هاتفي</span>
                      </a>
                    </div>
                  </div>
                )}

                {/* 4. زر الإجراء الرئيسي للسيارة */}
                {isAvailable ? (
                  <button
                    type="button"
                    className="btn-primary-action"
                    onClick={() => onRentCarClick(v)}
                    style={{
                      width: '100%',
                      marginTop: '12px',
                      padding: '11px 16px',
                      fontSize: '0.92rem',
                      borderRadius: '10px'
                    }}
                  >
                    <Key size={17} color="var(--wood-gold-light)" />
                    <span>+ تأجير هذه المركبة الآن</span>
                  </button>
                ) : isRented ? (
                  <div style={{
                    marginTop: '12px',
                    textAlign: 'center',
                    background: '#fef2f2',
                    color: '#991b1b',
                    border: '1.5px solid #ef4444',
                    borderRadius: '10px',
                    padding: '9px 12px',
                    fontSize: '0.84rem',
                    fontWeight: 900,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: '6px',
                    boxShadow: '0 1px 3px rgba(239, 68, 68, 0.1)'
                  }}>
                    <span style={{ width: '8px', height: '8px', borderRadius: '50%', background: '#ef4444', display: 'inline-block' }}></span>
                    <span>المركبة مؤجرة حالياً بعقد نشط</span>
                  </div>
                ) : (
                  <div style={{
                    marginTop: '10px',
                    textAlign: 'center',
                    background: '#fffbeb',
                    color: '#b45309',
                    border: '1.5px solid #fcd34d',
                    borderRadius: '8px',
                    padding: '8px',
                    fontSize: '0.82rem',
                    fontWeight: 900
                  }}>
                    ⛔ متوقفة بالصيانة (ممنوعة من التأجير)
                  </div>
                )}
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}
