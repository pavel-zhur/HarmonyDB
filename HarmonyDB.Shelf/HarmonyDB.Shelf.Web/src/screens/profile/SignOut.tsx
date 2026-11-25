// Signing out, by the person. With changes not sent yet (marks, keys, notes — a note's text among them), they are told
// and choose: send them now (with a connection), sign out and lose them (nothing is left on this device), or stay.

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { signOut } from '../../data/session';
import { local, useSyncStatus } from '../../local';

export function SignOutButton({ className = 'btn', label }: { className?: string; label?: string }) {
    const { t } = useTranslation();
    const { queued } = useSyncStatus();
    const [asking, setAsking] = useState(false);
    const [busy, setBusy] = useState(false);
    const [failed, setFailed] = useState(false);

    const sendAndGo = async () => {
        setBusy(true);
        setFailed(false);
        await local.exchange();
        setBusy(false);
        // what is still waiting did not go: the person chooses again
        if (local.pendingCount() > 0) setFailed(true);
        else void signOut();
    };
    const loseAndGo = () => {
        local.forgetUnsent();
        void signOut();
    };

    if (!asking) {
        return <button type="button" className={className} onClick={() => (queued > 0 ? setAsking(true) : void signOut())}>{label ?? t('profile.signOut')}</button>;
    }
    return (
        <div className="confirm pf-out">
            <p>{t('account.signOut.unsent', { count: queued })}</p>
            {failed && <p className="failed">{t('account.signOut.notSent')}</p>}
            <div className="chips">
                {navigator.onLine && <button type="button" className="btn pri" disabled={busy} onClick={() => void sendAndGo()}>{t('account.signOut.send')}</button>}
                <button type="button" className="btn dan" disabled={busy} onClick={loseAndGo}>{t('account.signOut.lose')}</button>
                <button type="button" className="btn" disabled={busy} onClick={() => setAsking(false)}>{t('account.signOut.stay')}</button>
            </div>
        </div>
    );
}
