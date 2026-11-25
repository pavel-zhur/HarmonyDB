// The small pieces of the design (styles/base.css): switch, segmented choice, stepper, chip, "soon", the search
// field, the avatar, the inline confirmation of a dangerous step.

import { useEffect, useRef, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon, type IconName } from './Icon';

export function Switch({ on, onChange, label, disabled }: { on: boolean; onChange: (on: boolean) => void; label: string; disabled?: boolean }) {
    return <button type="button" className={on ? 'sw on' : 'sw'} role="switch" aria-checked={on} aria-label={label} title={label} disabled={disabled} onClick={() => onChange(!on)} />;
}

export function Seg<T extends string>({ value, options, onChange, disabled }: { value: T; options: [T, string][]; onChange: (value: T) => void; disabled?: boolean }) {
    return (
        <span className="seg">
            {options.map(([key, label]) => (
                <button type="button" key={key} className={key === value ? 'on' : ''} disabled={disabled} aria-pressed={key === value} onClick={() => key !== value && onChange(key)}>{label}</button>
            ))}
        </span>
    );
}

/** − value +; a tap on the value resets it. */
export function Stepper({ value, onStep, onReset, lessLabel, moreLabel, resetLabel }: {
    value: string; onStep: (direction: -1 | 1) => void; onReset: () => void; lessLabel: string; moreLabel: string; resetLabel: string;
}) {
    return (
        <span className="step">
            <button type="button" aria-label={lessLabel} onClick={() => onStep(-1)}>−</button>
            <span className="v" role="button" tabIndex={0} title={resetLabel} onClick={onReset} onKeyDown={e => e.key === 'Enter' && onReset()}>{value}</span>
            <button type="button" aria-label={moreLabel} onClick={() => onStep(1)}>+</button>
        </span>
    );
}

export function Chip({ on, onClick, children, title, icon }: { on?: boolean; onClick: (e: React.MouseEvent<HTMLButtonElement>) => void; children: ReactNode; title?: string; icon?: IconName }) {
    return (
        <button type="button" className={on ? 'chip on' : 'chip'} title={title} onClick={onClick}>
            {icon && <Icon name={icon} />}
            {children}
        </button>
    );
}

/** Marks a thing that is there but does nothing yet. */
export function Soon() {
    const { t } = useTranslation();
    return <span className="soon">{t('common.soon')}</span>;
}

/**
 * The search field: filters as one types, the cursor stays where it is (the value is the caller's state; the input
 * is never re-created).
 */
export function SearchField({ value, onChange, placeholder, autoFocus }: { value: string; onChange: (value: string) => void; placeholder: string; autoFocus?: boolean }) {
    const { t } = useTranslation();
    const input = useRef<HTMLInputElement>(null);
    return (
        <div className="search">
            <Icon name="search" />
            <input ref={input} className="field" type="text" inputMode="search" enterKeyHint="search" placeholder={placeholder} autoComplete="off" autoFocus={autoFocus}
                value={value} onChange={e => onChange(e.target.value)} />
            {value && (
                <button type="button" className="x" aria-label={t('common.clear')} onClick={() => { onChange(''); input.current?.focus(); }}>
                    <Icon name="x" />
                </button>
            )}
        </div>
    );
}

const hue = (id: string) => [...id].reduce((h, c) => (h * 31 + c.charCodeAt(0)) % 360, 7);

/** A person: their picture, or the first letter on a colour of their own. */
export function Avatar({ person, size }: { person: { id: string; name: string; pictureUrl?: string | null }; size?: 's' | 'l' }) {
    const className = size ? `av ${size}` : 'av';
    if (person.pictureUrl) return <img className={className} src={person.pictureUrl} alt="" />;
    return <span className={className} style={{ background: `hsl(${hue(person.id)} 55% 48%)` }} aria-hidden="true">{person.name.trim().charAt(0).toUpperCase()}</span>;
}

/** A dangerous step asked again in place: the text and the choices, each in red, and Cancel. */
export function Confirm({ text, choices, onCancel }: { text: string; choices: [string, () => void][]; onCancel: () => void }) {
    const { t } = useTranslation();
    // asked where it was pressed: brought whole into view (at a page's bottom, its buttons would be below the screen)
    const box = useRef<HTMLDivElement>(null);
    useEffect(() => box.current?.scrollIntoView({ block: 'nearest', behavior: 'smooth' }), []);
    return (
        <div className="confirm" ref={box}>
            <p>{text}</p>
            <div className="chips">
                {choices.map(([label, run]) => <button type="button" key={label} className="btn dan" onClick={run}>{label}</button>)}
                <button type="button" className="btn" onClick={onCancel}>{t('common.cancel')}</button>
            </div>
        </div>
    );
}
