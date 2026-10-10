import React, { useState, useEffect } from 'react';
import { Delete, Clock, ShieldCheck, RefreshCw, Eye, EyeOff } from 'lucide-react';
import { supabase } from '../supabaseClient';

const LOCAL_FALLBACK_USERS = [
  { username: 'admin', fullName: 'حسين (المدير العام)', role: 'Admin', pin: '9999' },
  { username: 'staff', fullName: 'موظف الاستقبال', role: 'Staff', pin: '5678' },
  { username: 'accountant', fullName: 'المحاسب المالي', role: 'Accountant', pin: '0000' }
];

export default function LoginScreen({ onLoginSuccess, installPrompt, onInstallClick }) {
  const [pin, setPin] = useState('');
  const [showPin, setShowPin] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [hasError, setHasError] = useState(false);
  const [isAuthenticating, setIsAuthenticating] = useState(false);
  const [currentTimeString, setCurrentTimeString] = useState('');
  const [currentDateString, setCurrentDateString] = useState('');

  // ساعة وتاريخ عربي مطابق لـ LoginScreen.razor تماماً
  useEffect(() => {
    const updateDateTime = () => {
      const now = new Date();
      setCurrentTimeString(now.toLocaleTimeString('ar-EG', { hour: '2-digit', minute: '2-digit' }));

      const days = ['الأحد', 'الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];
      const months = [
        'يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو',
        'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر'
      ];
      const dayName = days[now.getDay()];
      const monthName = months[now.getMonth()];
      setCurrentDateString(`${dayName}، ${now.getDate()} ${monthName} ${now.getFullYear()}`);
    };

    updateDateTime();
    const timer = setInterval(updateDateTime, 10000);
    return () => clearInterval(timer);
  }, []);

  // دعم الكتابة المباشرة من لوحة المفاتيح
  useEffect(() => {
    const handleKeyDown = (e) => {
      if (isAuthenticating) return;
      if (e.key >= '0' && e.key <= '9') {
        handleAppendDigit(e.key);
      } else if (e.key === 'Backspace') {
        handleBackspace();
      } else if (e.key === 'Escape') {
        handleClearPin();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [pin, isAuthenticating]);

  const handleAppendDigit = (digit) => {
    if (isAuthenticating) return;

    if (pin.length < 4) {
      const nextPin = pin + digit;
      setPin(nextPin);
      setErrorMessage('');
      setHasError(false);

      if (nextPin.length === 4) {
        setTimeout(() => {
          attemptLogin(nextPin);
        }, 120);
      }
    }
  };

  const handleBackspace = () => {
    if (isAuthenticating) return;
    if (pin.length > 0) {
      setPin(prev => prev.slice(0, -1));
      setErrorMessage('');
      setHasError(false);
    }
  };

  const handleClearPin = () => {
    if (isAuthenticating) return;
    setPin('');
    setErrorMessage('');
    setHasError(false);
  };

  // التحقق المباشر من الـ PIN الحي من جدول users في السحابة
  const attemptLogin = async (enteredPin) => {
    setIsAuthenticating(true);
    setErrorMessage('');
    setHasError(false);

    try {
      let matchedUser = null;
      let cloudQueriedSuccessfully = false;

      // 1. الفحص الحي في Supabase مباشرة (Live Cloud Authentication)
      if (supabase) {
        try {
          const { data, error } = await supabase
            .from('users')
            .select('username, full_name, role, pin_code, is_active')
            .eq('pin_code', enteredPin)
            .eq('is_active', true)
            .maybeSingle();

          if (!error) {
            cloudQueriedSuccessfully = true;
            if (data) {
              matchedUser = {
                username: data.username,
                fullName: data.full_name,
                role: data.role
              };

              // تحديث ذاكرة التخزين المحلية للمستخدمين المعتمدين لضمان عمل الأوفلاين مستقبلاً بنفس الرمز
              try {
                const cachedUsers = JSON.parse(localStorage.getItem('CACHED_SYSTEM_USERS') || '[]');
                const filtered = cachedUsers.filter(u => u.username !== data.username);
                filtered.push({
                  username: data.username,
                  fullName: data.full_name,
                  role: data.role,
                  pin: enteredPin
                });
                localStorage.setItem('CACHED_SYSTEM_USERS', JSON.stringify(filtered));
              } catch { }
            }
          }
        } catch (dbErr) {
          console.warn('Supabase auth query failed, will check cached fallback:', dbErr);
        }
      }

      // 2. إذا كانت السحابة متصلة واستجابت ولم تجد أي حساب بهذا الرمز -> الرمز خاطئ قطيعاً!
      if (cloudQueriedSuccessfully && !matchedUser) {
        setIsAuthenticating(false);
        setHasError(true);
        setErrorMessage('رمز المرور غير صحيح! يرجى إدخال الرمز السري المعتمد في النظام.');
        setPin('');
        return;
      }

      // 3. فقط في حالة انقطاع الاتصال بالإنترنت تماماً، نتحقق من الذاكرة المحلية
      if (!matchedUser && !cloudQueriedSuccessfully) {
        try {
          const cachedUsers = JSON.parse(localStorage.getItem('CACHED_SYSTEM_USERS') || '[]');
          const local = cachedUsers.find(u => u.pin === enteredPin) || LOCAL_FALLBACK_USERS.find(u => u.pin === enteredPin);
          if (local) {
            matchedUser = {
              username: local.username,
              fullName: local.fullName,
              role: local.role
            };
          }
        } catch { }
      }

      if (matchedUser) {
        setIsAuthenticating(false);
        onLoginSuccess(matchedUser);
      } else {
        setIsAuthenticating(false);
        setHasError(true);
        setErrorMessage('رمز المرور غير صحيح! يرجى إدخال الرمز السري المعتمد في النظام.');
        setPin('');
      }
    } catch {
      setIsAuthenticating(false);
      setHasError(true);
      setErrorMessage('تعذر التحقق من الرمز حالياً، يرجى المحاولة ثانية.');
      setPin('');
    }
  };

  return (
    <div style={{
      minHeight: '100vh',
      width: '100vw',
      background: 'radial-gradient(circle at 50% 12%, rgba(212, 175, 55, 0.16) 0%, transparent 45%), radial-gradient(circle at 10% 88%, rgba(2, 132, 199, 0.10) 0%, transparent 35%), linear-gradient(135deg, #fdfbf7 0%, #f6f1e6 50%, #eee4d1 100%)',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      padding: '24px 16px',
      position: 'relative',
      direction: 'rtl',
      overflowX: 'hidden',
      userSelect: 'none'
    }}>
      {/* 1. الشريط العلوي التنفيذي (Live Status & Clock) */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        width: '100%',
        maxWidth: '420px',
        marginBottom: '16px',
        padding: '0 4px',
        zIndex: 10
      }}>
        <div style={{
          display: 'flex',
          alignItems: 'center',
          gap: '8px',
          background: 'rgba(255, 255, 255, 0.9)',
          border: '1px solid var(--border-color)',
          padding: '6px 14px',
          borderRadius: '30px',
          fontSize: '0.78rem',
          fontWeight: 800,
          color: 'var(--wood-walnut)',
          boxShadow: '0 2px 8px rgba(45, 24, 12, 0.05)'
        }}>
          <span className="pulse-dot" />
          <span>TAJ SYSTEM • بوابة الدخول الآمن</span>
        </div>

        <div style={{
          display: 'flex',
          alignItems: 'center',
          gap: '6px',
          background: 'rgba(255, 255, 255, 0.9)',
          border: '1px solid var(--border-color)',
          padding: '6px 14px',
          borderRadius: '30px',
          fontSize: '0.76rem',
          fontWeight: 800,
          color: 'var(--text-muted)'
        }}>
          <Clock size={13} color="var(--wood-amber)" />
          <span>{currentTimeString}</span>
        </div>
      </div>

      {/* 2. بطاقة تسجيل الدخول الرئيسية (مطابقة لـ LoginScreen.razor 100%) */}
      <div className="login-executive-card" style={{
        position: 'relative',
        background: '#ffffff',
        border: '2px solid var(--border-color)',
        borderRadius: '26px',
        boxShadow: '0 24px 60px rgba(60, 35, 18, 0.12), inset 0 1px 0 #ffffff',
        padding: '32px 28px 24px 28px',
        width: '100%',
        maxWidth: '420px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '16px',
        zIndex: 5
      }}>
        {/* شريط الخشب والتطعيم الذهبي العلوي */}
        <div className="login-wood-inlay" />

        {/* الشعار والترويسة التنفيذية */}
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center', gap: '6px' }}>
          <div style={{
            width: '94px',
            height: '94px',
            borderRadius: '50%',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            filter: 'drop-shadow(0 8px 20px rgba(45, 24, 12, 0.22))',
            marginBottom: '4px'
          }}>
            <img
              src="/images/logo.png"
              alt="TAJ SYSTEM"
              style={{
                width: '94px',
                height: '94px',
                objectFit: 'contain',
                borderRadius: '50%',
                background: '#ffffff',
                border: '2px solid var(--wood-gold-inlay)'
              }}
              onError={(e) => {
                e.target.style.display = 'none';
              }}
            />
          </div>

          <h1 style={{
            fontSize: '1.65rem',
            fontWeight: 900,
            color: 'var(--wood-walnut)',
            letterSpacing: '0.8px',
            margin: 0
          }}>
            TAJ SYSTEM
          </h1>

          <div style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '6px',
            fontSize: '0.78rem',
            fontWeight: 800,
            color: 'var(--wood-amber)',
            background: 'var(--accent-gold-bg)',
            border: '1px solid rgba(212, 175, 55, 0.35)',
            padding: '3px 12px',
            borderRadius: '20px'
          }}>
            <span>✦</span>
            <span>نظام التاج لإدارة وتأجير السيارات</span>
            <span>✦</span>
          </div>

          <p style={{
            fontSize: '0.82rem',
            fontWeight: 600,
            color: 'var(--text-muted)',
            margin: '4px 0 0 0'
          }}>
            أدخل الرمز السري للموظف (4 أرقام) للوصول للوحة التحكم
          </p>
        </div>

        {/* 3. مؤشرات الـ PIN الأربعة (Strictly RTL Layout) */}
        <div style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: '8px',
          width: '100%'
        }}>
          <div style={{
            display: 'flex',
            justifyContent: 'center',
            alignItems: 'center',
            gap: '16px',
            padding: '4px 0',
            direction: 'rtl'
          }}>
            {[0, 1, 2, 3].map((i) => {
              const isFilled = pin.length > i;
              const isCurrent = pin.length === i;

              return (
                <div
                  key={i}
                  style={{
                    width: '34px',
                    height: '34px',
                    borderRadius: '50%',
                    border: `2.5px solid ${isFilled ? '#0284c7' : isCurrent ? 'var(--accent-gold)' : 'var(--border-color)'}`,
                    background: isFilled
                      ? 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)'
                      : '#fbf9f5',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    boxShadow: isFilled
                      ? '0 4px 14px rgba(2, 132, 199, 0.45)'
                      : isCurrent
                      ? '0 0 0 4px rgba(212, 175, 55, 0.2)'
                      : 'inset 0 2px 4px rgba(90, 70, 40, 0.08)',
                    transform: isFilled ? 'scale(1.12)' : 'scale(1)',
                    transition: 'all 0.2s cubic-bezier(0.34, 1.56, 0.64, 1)'
                  }}
                >
                  {isFilled && (
                    showPin ? (
                      <span style={{ fontSize: '1.15rem', fontWeight: 800, color: '#ffffff', lineHeight: 1 }}>
                        {pin[i]}
                      </span>
                    ) : (
                      <span style={{
                        width: '10px',
                        height: '10px',
                        borderRadius: '50%',
                        background: '#ffffff',
                        boxShadow: '0 0 6px #ffffff'
                      }} />
                    )
                  )}
                </div>
              );
            })}
          </div>

          {/* زر كشف/إخفاء الرمز */}
          <button
            type="button"
            onClick={() => setShowPin(!showPin)}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '5px',
              background: 'transparent',
              border: 'none',
              color: 'var(--text-muted)',
              fontSize: '0.76rem',
              fontWeight: 700,
              cursor: 'pointer',
              padding: '4px 10px',
              borderRadius: '12px'
            }}
          >
            {showPin ? <EyeOff size={15} /> : <Eye size={15} />}
            <span>{showPin ? 'إخفاء الرمز' : 'كشف الرمز'}</span>
          </button>
        </div>

        {/* رسائل التنبيه والخطأ */}
        {errorMessage && (
          <div className="alert-banner alert-danger" style={{ width: '100%', justifyContent: 'center' }}>
            <span>⚠️ {errorMessage}</span>
          </div>
        )}

        {isAuthenticating && (
          <div style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            color: 'var(--primary-blue)',
            fontSize: '0.84rem',
            fontWeight: 800
          }}>
            <RefreshCw size={15} className="spin" />
            <span>جاري التحقق من الرمز المباشر...</span>
          </div>
        )}

        {/* 4. لوحة المفاتيح الرقمية (3x4 Keypad - Strict RTL) */}
        <div className="keypad-grid-system" style={{ marginTop: '4px' }}>
          {/* Row 1: 1 (Right) | 2 (Center) | 3 (Left) */}
          <button type="button" className="keypad-btn-system" onClick={() => handleAppendDigit('1')}>
            <span className="main-digit-txt">1</span>
            <span className="sub-digit-txt">١</span>
          </button>
          <button type="button" className="keypad-btn-system" onClick={() => handleAppendDigit('2')}>
            <span className="main-digit-txt">2</span>
            <span className="sub-digit-txt">٢</span>
          </button>
          <button type="button" className="keypad-btn-system" onClick={() => handleAppendDigit('3')}>
            <span className="main-digit-txt">3</span>
            <span className="sub-digit-txt">٣</span>
          </button>

          {/* Row 2: 4 (Right) | 5 (Center) | 6 (Left) */}
          <button type="button" className="keypad-btn-system" onClick={() => handleAppendDigit('4')}>
            <span className="main-digit-txt">4</span>
            <span className="sub-digit-txt">٤</span>
          </button>
          <button type="button" className="keypad-btn-system" onClick={() => handleAppendDigit('5')}>
            <span className="main-digit-txt">5</span>
            <span className="sub-digit-txt">٥</span>
          </button>
          <button type="button" className="keypad-btn-system" onClick={() => handleAppendDigit('6')}>
            <span className="main-digit-txt">6</span>
            <span className="sub-digit-txt">٦</span>
          </button>

          {/* Row 3: 7 (Right) | 8 (Center) | 9 (Left) */}
          <button type="button" className="keypad-btn-system" onClick={() => handleAppendDigit('7')}>
            <span className="main-digit-txt">7</span>
            <span className="sub-digit-txt">٧</span>
          </button>
          <button type="button" className="keypad-btn-system" onClick={() => handleAppendDigit('8')}>
            <span className="main-digit-txt">8</span>
            <span className="sub-digit-txt">٨</span>
          </button>
          <button type="button" className="keypad-btn-system" onClick={() => handleAppendDigit('9')}>
            <span className="main-digit-txt">9</span>
            <span className="sub-digit-txt">٩</span>
          </button>

          {/* Row 4: C (مسح) | 0 | ⌫ (تراجع) */}
          <button type="button" className="keypad-btn-system keypad-clear-btn" onClick={handleClearPin} title="مسح">
            <span className="main-digit-txt" style={{ color: '#dc2626' }}>C</span>
            <span className="sub-digit-txt" style={{ color: '#dc2626' }}>مسح</span>
          </button>
          <button type="button" className="keypad-btn-system" onClick={() => handleAppendDigit('0')}>
            <span className="main-digit-txt">0</span>
            <span className="sub-digit-txt">٠</span>
          </button>
          <button type="button" className="keypad-btn-system keypad-backspace-btn" onClick={handleBackspace} title="تراجع">
            <Delete size={22} color="var(--wood-walnut)" />
            <span className="sub-digit-txt">تراجع</span>
          </button>
        </div>

        {/* زر تثبيت التطبيق على الهاتف PWA */}
        {installPrompt && (
          <button
            type="button"
            onClick={onInstallClick}
            style={{
              background: 'linear-gradient(135deg, #2d180c 0%, #442211 100%)',
              border: '1.5px solid var(--wood-gold-inlay)',
              borderRadius: '12px',
              padding: '10px 18px',
              color: '#ffffff',
              fontSize: '0.84rem',
              fontWeight: 800,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
              boxShadow: '0 4px 14px rgba(45, 24, 12, 0.25)',
              margin: '4px 0'
            }}
          >
            <span>📲</span>
            <span>تثبيت تطبيق التاج على الهاتف</span>
          </button>
        )}

        {/* Footer info */}
        <div style={{
          display: 'flex',
          alignItems: 'center',
          gap: '6px',
          fontSize: '0.72rem',
          color: 'var(--text-muted)',
          fontWeight: 700,
          marginTop: '6px'
        }}>
          <ShieldCheck size={14} color="var(--accent-gold)" />
          <span>اتصال سحابي آمن ومشفر • مزامنة تلقائية مع قاعدة البيانات</span>
        </div>
      </div>
    </div>
  );
}
