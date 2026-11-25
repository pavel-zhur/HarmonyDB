import { useTranslation } from 'react-i18next';
import { dismissToast, useToast } from '../app/toasts';

/** Shows the current toast (app/toasts.ts): "Undo" when the change can be undone, or its own action. */
export function Toaster() {
    const { t } = useTranslation();
    const current = useToast();
    if (!current) return null;
    const button = current.undo ? { label: t('common.undo'), run: current.undo } : current.action;
    return (
        <div className="toast" role="status" key={current.id}>
            <span>{current.text}</span>
            {button && <button type="button" onClick={() => { dismissToast(); button.run(); }}>{button.label}</button>}
        </div>
    );
}
