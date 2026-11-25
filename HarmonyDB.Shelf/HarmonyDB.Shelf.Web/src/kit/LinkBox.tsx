import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from '../app/toasts';
import { Qr } from './Qr';

/** The link with Copy and its QR code; `children`: what stands between them (the share box's buttons). */
export function LinkBox({ link, note, children }: { link: string; note?: string; children?: ReactNode }) {
    const { t } = useTranslation();
    const copy = async () => {
        await navigator.clipboard.writeText(link);
        toast(t('common.copied'));
    };
    return (
        <>
            <div className="linkbox"><code>{link}</code><button type="button" className="btn" onClick={() => void copy()}>{t('common.copy')}</button></div>
            {children}
            <div className="qr"><Qr text={link} />{note && <div className="small muted">{note}</div>}</div>
        </>
    );
}
