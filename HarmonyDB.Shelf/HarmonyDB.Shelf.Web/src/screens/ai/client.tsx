// Who an assistant is, as the person is shown it — everywhere a client is named (the consent, "Your AI"). The hosts
// tell clients apart, so they come first, plain, each on its own line, exactly as the server gives them (in a face
// where look-alike letters differ; never turned into other letters by the page). The client's name is only what it
// calls itself: anyone may take a well-known one; it is shown after the hosts, on its own line, marked so, without
// control and direction characters, its spaces made plain, clipped to a sane length.

import { useTranslation } from 'react-i18next';

/** The longest name shown, characters. */
const nameLength = 60;

/** The name a client gave, made safe to show; null when nothing is left of it. */
export function cleanName(name: string | null | undefined): string | null {
    if (!name) return null;
    const plain = name
        // controls, format characters (direction marks and overrides, zero widths), separators of lines
        .replace(/[\p{Cc}\p{Cf}\p{Zl}\p{Zp}]/gu, '')
        // every kind of space a plain one, one at a time
        .replace(/\p{Zs}+/gu, ' ')
        .trim();
    if (!plain) return null;
    const letters = [...plain];
    return letters.length > nameLength ? letters.slice(0, nameLength).join('') + '…' : plain;
}

/** The host that stands for a client in a sentence: the address it is known by, else where it goes back to. */
export function clientHost(client: { clientIdHost?: string | null; redirectHosts?: readonly string[]; redirectHost?: string }): string {
    return client.clientIdHost ?? client.redirectHost ?? client.redirectHosts?.[0] ?? '';
}

/** The hosts first, then the name the client calls itself. */
export function ClientIdentity({ idHost, redirectHosts, name }: { idHost: string | null; redirectHosts: readonly string[]; name: string | null | undefined }) {
    const { t } = useTranslation();
    const shown = cleanName(name);
    return (
        <span className="ai-ident">
            {idHost && <span className="ai-hl"><span className="ai-hk">{t('ai.hosts.id')}</span> <code className="ai-host">{idHost}</code></span>}
            <span className="ai-hl"><span className="ai-hk">{t('ai.hosts.back')}</span> {redirectHosts.map((host, i) => <span key={host}>{i > 0 && ', '}<code className="ai-host">{host}</code></span>)}</span>
            {shown && <span className="ai-calls">{t('ai.hosts.calls', { name: shown })}</span>}
        </span>
    );
}
