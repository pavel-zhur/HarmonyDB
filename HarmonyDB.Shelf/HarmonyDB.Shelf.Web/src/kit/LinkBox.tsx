import { useTranslation } from 'react-i18next';
import { toast } from '../app/toasts';
import { Qr } from './Qr';

/** A link to give: the address, Copy, its QR code and a note beside it. */
export function LinkBox({ link, note }: { link: string; note?: string }) {
    const { t } = useTranslation();
    const copy = async () => {
        await navigator.clipboard.writeText(link);
        toast(t('common.copied'));
    };
    return (
        <>
            <div className="linkbox"><code>{link}</code><button type="button" className="btn" onClick={() => void copy()}>{t('common.copy')}</button></div>
            <div className="qr"><Qr text={link} />{note && <div className="small muted">{note}</div>}</div>
        </>
    );
}
