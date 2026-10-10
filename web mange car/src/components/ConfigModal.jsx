import React, { useState, useEffect } from 'react';
import { Database, CheckCircle, AlertTriangle, X, Key, Globe, RefreshCw, ExternalLink } from 'lucide-react';
import { supabase, updateSupabaseConfig, isSupabaseConfigured } from '../supabaseClient';

export default function ConfigModal({ isOpen, onClose, onConfigSaved }) {
  const [url, setUrl] = useState('');
  const [key, setKey] = useState('');
  const [isTesting, setIsTesting] = useState(false);
  const [testResult, setTestResult] = useState(null); // { success: boolean, message: string }

  useEffect(() => {
    const savedUrl = localStorage.getItem('CAR_RENTAL_SUPABASE_URL') || import.meta.env.VITE_SUPABASE_URL || '';
    const savedKey = localStorage.getItem('CAR_RENTAL_SUPABASE_KEY') || import.meta.env.VITE_SUPABASE_ANON_KEY || '';
    setUrl(savedUrl);
    setKey(savedKey);
  }, [isOpen]);

  const handleTestAndSave = async (e) => {
    e.preventDefault();
    if (!url.trim() || !key.trim()) {
      setTestResult({ success: false, message: 'يرجى إدخال رابط المشروع والمفتاح العام (Anon Key) معاً!' });
      return;
    }

    setIsTesting(true);
    setTestResult(null);

    try {
      const ok = updateSupabaseConfig(url.trim(), key.trim());
      if (!ok) {
        throw new Error('صيغة الرابط أو المفتاح غير صالحة');
      }

      // التحقق من الاتصال الفعلي بالجدول
      const { data, error } = await supabase.from('vehicles').select('id').limit(1);

      if (error) {
        throw error;
      }

      setTestResult({
        success: true,
        message: 'تم الاتصال بقاعدة بيانات سوبابيز بنجاح تام! جاهز لمزامنة السيارات والعقود.'
      });

      if (onConfigSaved) {
        onConfigSaved();
      }

      setTimeout(() => {
        onClose();
      }, 1400);
    } catch (err) {
      console.error('Supabase test failed:', err);
      setTestResult({
        success: false,
        message: `تعذر الاتصال بقاعدة البيانات: ${err.message || 'تأكد من تطبيق سكريبت SQL أولاً في Supabase'}`
      });
    } finally {
      setIsTesting(false);
    }
  };

  const handleClearConfig = () => {
    localStorage.removeItem('CAR_RENTAL_SUPABASE_URL');
    localStorage.removeItem('CAR_RENTAL_SUPABASE_KEY');
    setUrl('');
    setKey('');
    setTestResult({
      success: true,
      message: 'تم تفعيل وضع المعاينة المحلي (Demo Mode) بنجاح.'
    });
    if (onConfigSaved) onConfigSaved();
  };

  if (!isOpen) return null;

  return (
    <div style={{
      position: 'fixed',
      top: 0,
      left: 0,
      right: 0,
      bottom: 0,
      background: 'rgba(0, 0, 0, 0.85)',
      backdropFilter: 'blur(8px)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      zIndex: 9999,
      padding: '16px'
    }}>
      <div style={{
        background: '#111827',
        border: '1px solid rgba(255, 255, 255, 0.12)',
        borderRadius: '20px',
        width: '100%',
        maxWidth: '480px',
        padding: '24px',
        boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.7)',
        direction: 'rtl'
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '18px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div style={{
              width: '40px',
              height: '40px',
              borderRadius: '10px',
              background: 'linear-gradient(135deg, #10b981 0%, #059669 100%)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center'
            }}>
              <Database size={22} color="#fff" />
            </div>
            <div>
              <h2 style={{ fontSize: '1.15rem', fontWeight: 800, margin: 0, color: '#f8fafc' }}>
                ربط سحابة Supabase
              </h2>
              <span style={{ fontSize: '0.78rem', color: '#94a3b8' }}>
                المزامنة الحية للأسطول والعقود
              </span>
            </div>
          </div>
          <button
            onClick={onClose}
            style={{
              background: 'rgba(255, 255, 255, 0.05)',
              border: 'none',
              borderRadius: '50%',
              width: '34px',
              height: '34px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              cursor: 'pointer',
              color: '#94a3b8'
            }}
          >
            <X size={18} />
          </button>
        </div>

        <p style={{ fontSize: '0.85rem', color: '#cbd5e1', lineHeight: '1.5', marginBottom: '16px' }}>
          أدخل بيانات مشروعك من لوحة تحكم <strong>Supabase</strong> (من قائمة Project Settings ➔ API):
        </p>

        <form onSubmit={handleTestAndSave} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
          <div>
            <label className="form-label" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Globe size={14} color="#3b82f6" />
              رابط المشروع (Project URL):
            </label>
            <input
              type="url"
              className="form-input"
              dir="ltr"
              placeholder="https://xyzabcdefg.supabase.co"
              value={url}
              onChange={e => setUrl(e.target.value)}
              required
            />
          </div>

          <div>
            <label className="form-label" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Key size={14} color="#f59e0b" />
              المفتاح العام (anon / public key):
            </label>
            <input
              type="password"
              className="form-input"
              dir="ltr"
              placeholder="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
              value={key}
              onChange={e => setKey(e.target.value)}
              required
            />
          </div>

          {testResult && (
            <div style={{
              padding: '12px',
              borderRadius: '10px',
              background: testResult.success ? 'rgba(16, 185, 129, 0.1)' : 'rgba(239, 68, 68, 0.1)',
              border: `1px solid ${testResult.success ? '#10b981' : '#ef4444'}`,
              display: 'flex',
              alignItems: 'center',
              gap: '10px',
              fontSize: '0.82rem',
              color: testResult.success ? '#34d399' : '#f87171'
            }}>
              {testResult.success ? <CheckCircle size={18} /> : <AlertTriangle size={18} />}
              <span>{testResult.message}</span>
            </div>
          )}

          <div style={{ display: 'flex', gap: '10px', marginTop: '6px' }}>
            <button
              type="submit"
              disabled={isTesting}
              style={{
                flex: 2,
                background: 'linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%)',
                color: '#fff',
                border: 'none',
                borderRadius: '12px',
                padding: '12px',
                fontWeight: 700,
                fontSize: '0.9rem',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: '8px',
                boxShadow: '0 4px 12px rgba(37, 99, 235, 0.3)'
              }}
            >
              {isTesting ? (
                <>
                  <RefreshCw size={16} className="spin" />
                  جاري اختبار الاتصال...
                </>
              ) : (
                'حفظ واختبار الاتصال'
              )}
            </button>

            <button
              type="button"
              onClick={handleClearConfig}
              style={{
                flex: 1,
                background: 'rgba(255, 255, 255, 0.05)',
                color: '#94a3b8',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                borderRadius: '12px',
                padding: '12px',
                fontWeight: 600,
                fontSize: '0.85rem',
                cursor: 'pointer'
              }}
            >
              وضع تجريبي
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
