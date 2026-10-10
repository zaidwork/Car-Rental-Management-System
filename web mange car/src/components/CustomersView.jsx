import React, { useState } from 'react';
import { Search, Phone, MessageSquare, Key, ShieldAlert, CreditCard, User, CheckCircle2, Trash2 } from 'lucide-react';

export default function CustomersView({ customers, onRentForCustomer, currentUser, onDeleteCustomer }) {
  const [search, setSearch] = useState('');

  const filtered = customers.filter(c => {
    if (!search) return true;
    const q = search.toLowerCase();
    return (
      (c.full_name || '').toLowerCase().includes(q) ||
      (c.national_id || '').includes(q) ||
      (c.primary_phone || '').includes(q)
    );
  });

  return (
    <div style={{ padding: '14px 16px 20px 16px' }}>
      {/* 1. شريط البحث الملكي */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        background: '#ffffff',
        border: '1.5px solid var(--border-color)',
        borderRadius: '12px',
        padding: '10px 14px',
        gap: '10px',
        boxShadow: '0 2px 6px rgba(45, 24, 12, 0.04)',
        marginBottom: '12px'
      }}>
        <Search size={18} color="var(--wood-amber)" />
        <input
          type="text"
          placeholder="بحث عن عميل بالاسم، رقم الهوية، أو الهاتف..."
          value={search}
          onChange={e => setSearch(e.target.value)}
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

      <div style={{
        fontSize: '0.82rem',
        color: 'var(--text-muted)',
        marginBottom: '12px',
        fontWeight: 800,
        display: 'flex',
        justifyContent: 'space-between',
        padding: '0 4px'
      }}>
        <span>إجمالي العملاء المسجلين:</span>
        <span style={{ color: 'var(--wood-walnut)', fontWeight: 900 }}>{filtered.length} عميل</span>
      </div>

      {/* 2. بطاقات العملاء التنفيذية */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
        {filtered.length === 0 ? (
          <div style={{
            textAlign: 'center',
            padding: '30px',
            background: '#ffffff',
            borderRadius: '16px',
            border: '1.5px solid var(--border-color)',
            color: 'var(--text-muted)',
            fontWeight: 700
          }}>
            لا يوجد عميل يطابق البحث.
          </div>
        ) : (
          filtered.map(c => {
            const hasDebts = Number(c.total_debts || 0) > 0;
            const isLicenseExpired = c.license_expiry_date && new Date(c.license_expiry_date) < new Date();

            return (
              <div
                key={c.id}
                style={{
                  background: '#ffffff',
                  border: '1.5px solid var(--border-color)',
                  borderRadius: '16px',
                  padding: '16px',
                  boxShadow: '0 4px 14px rgba(45, 24, 12, 0.06)',
                  position: 'relative'
                }}
              >
                {/* رأس البطاقة */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <div style={{
                      width: '42px',
                      height: '42px',
                      borderRadius: '12px',
                      background: 'linear-gradient(135deg, #ede4d3 0%, #e3d8c4 100%)',
                      border: '1.5px solid var(--border-color)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      color: 'var(--wood-amber)',
                      flexShrink: 0
                    }}>
                      <User size={22} />
                    </div>

                    <div>
                      <h3 style={{ fontSize: '1.05rem', fontWeight: 900, color: 'var(--wood-walnut)', margin: 0 }}>
                        {c.full_name}
                      </h3>
                      <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)', fontWeight: 700, marginTop: '2px' }}>
                        هوية: <span style={{ fontFamily: 'monospace', color: 'var(--primary-blue)', fontWeight: 800 }}>{c.national_id}</span>
                        {c.address && ` • ${c.address}`}
                      </div>
                    </div>
                  </div>

                  {/* شارة الذمم والديون */}
                  <div>
                    {hasDebts ? (
                      <span style={{
                        background: 'var(--accent-red-bg)',
                        color: 'var(--accent-red)',
                        border: '1px solid var(--accent-red-border)',
                        padding: '3px 8px',
                        borderRadius: '6px',
                        fontSize: '0.74rem',
                        fontWeight: 900,
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '4px'
                      }}>
                        <CreditCard size={12} />
                        عليه ذمم: {c.total_debts} ₪
                      </span>
                    ) : (
                      <span style={{
                        background: 'var(--accent-green-bg)',
                        color: 'var(--accent-green)',
                        border: '1px solid var(--accent-green-border)',
                        padding: '3px 8px',
                        borderRadius: '6px',
                        fontSize: '0.72rem',
                        fontWeight: 800,
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '4px'
                      }}>
                        <CheckCircle2 size={12} />
                        ذمة سليمة
                      </span>
                    )}
                  </div>
                </div>

                {/* تفاصيل الهاتف والرخصة */}
                <div style={{
                  display: 'grid',
                  gridTemplateColumns: '1fr 1fr',
                  gap: '8px',
                  marginTop: '12px',
                  background: '#fcfaf6',
                  border: '1px solid var(--border-subtle)',
                  borderRadius: '10px',
                  padding: '8px 10px',
                  fontSize: '0.78rem'
                }}>
                  <div>
                    <span style={{ color: 'var(--text-muted)', fontWeight: 700 }}>الهاتف: </span>
                    <span style={{ fontWeight: 800, color: 'var(--wood-walnut)' }}>{c.primary_phone}</span>
                  </div>
                  <div>
                    <span style={{ color: 'var(--text-muted)', fontWeight: 700 }}>الرخصة: </span>
                    <span style={{
                      fontWeight: 800,
                      color: isLicenseExpired ? 'var(--accent-red)' : 'var(--accent-green)'
                    }}>
                      {isLicenseExpired ? 'منتهية ⚠️' : 'سارية ✓'}
                    </span>
                  </div>
                </div>

                {/* أزرار الاتصال والتأجير السريع */}
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px', marginTop: '10px' }}>
                  <a
                    href={`https://wa.me/972${(c.primary_phone || '').replace(/^0/, '')}`}
                    target="_blank"
                    rel="noreferrer"
                    className="btn-whatsapp-action"
                    style={{ padding: '8px', fontSize: '0.8rem' }}
                  >
                    <MessageSquare size={15} />
                    <span>محادثة واتساب</span>
                  </a>

                  <a
                    href={`tel:${c.primary_phone || ''}`}
                    className="btn-call-action"
                    style={{ padding: '8px', fontSize: '0.8rem' }}
                  >
                    <Phone size={15} />
                    <span>اتصال هاتفي</span>
                  </a>
                </div>

                <button
                  type="button"
                  className="btn-primary-action"
                  onClick={() => onRentForCustomer(c)}
                  style={{
                    width: '100%',
                    marginTop: '8px',
                    padding: '9px',
                    fontSize: '0.86rem',
                    borderRadius: '8px'
                  }}
                >
                  <Key size={15} color="var(--wood-gold-light)" />
                  <span>+ فتح عقد تأجير لهذا العميل</span>
                </button>

                {currentUser?.role === 'Admin' && (
                  <button
                    type="button"
                    onClick={() => onDeleteCustomer && onDeleteCustomer(c)}
                    style={{
                      width: '100%',
                      marginTop: '6px',
                      padding: '7px',
                      fontSize: '0.78rem',
                      borderRadius: '8px',
                      background: 'rgba(239, 68, 68, 0.08)',
                      border: '1px solid rgba(239, 68, 68, 0.25)',
                      color: '#dc2626',
                      fontWeight: 700,
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      gap: '6px'
                    }}
                  >
                    <Trash2 size={14} />
                    <span>حذف ملف العميل نهائياً</span>
                  </button>
                )}
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}
