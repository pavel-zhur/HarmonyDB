// The MCP permissions in a person's words: a list to read, and a list to tick (the consent, a connected assistant, a
// key). Each permission is the server's key; its words are the app's (i18n ai.permission.*). A permission the app has
// no words for is shown by its key, never hidden.

import { useTranslation } from 'react-i18next';
import { i18n } from '../../i18n';
import { permissionKey } from '../../data/mcp';
import { Icon, type IconName } from '../../kit/Icon';

const icons: Record<string, IconName> = {
    'mcp.index': 'search',
    'mcp.library.read': 'lib',
    'mcp.library.write': 'pen',
    'mcp.pdf': 'file',
    'mcp.yandex.anonymous': 'vol',
    'mcp.yandex.linked': 'user',
    'mcp.youtube.anonymous': 'yt',
    'mcp.youtube.linked': 'user',
};

export function permissionTitle(permission: string): string {
    const key = `${permissionKey(permission)}.title`;
    return i18n.exists(key) ? i18n.t(key as 'ai.permission.mcp_index.title') : permission;
}

function permissionText(permission: string): string | null {
    const key = `${permissionKey(permission)}.text`;
    return i18n.exists(key) ? i18n.t(key as 'ai.permission.mcp_index.text') : null;
}

function Words({ permission }: { permission: string }) {
    const text = permissionText(permission);
    return <span><b>{permissionTitle(permission)}</b>{text && <span className="sub">{text}</span>}</span>;
}

export function PermissionList({ permissions }: { permissions: readonly string[] }) {
    return (
        <div className="lines ai-perms">
            {permissions.map(p => <div className="ai-perm" key={p}><Icon name={icons[p] ?? 'mcp'} /><Words permission={p} /></div>)}
        </div>
    );
}

/** Ticks, one per permission given; `asked` marks those the assistant asks for; `off` those the person cannot give. */
export function PermissionPicker({ grantable, chosen, onChange, asked, off }: {
    grantable: readonly string[]; chosen: readonly string[]; onChange: (chosen: string[]) => void; asked?: readonly string[]; off?: readonly string[];
}) {
    const { t } = useTranslation();
    const toggle = (p: string) => onChange(chosen.includes(p) ? chosen.filter(x => x !== p) : grantable.filter(x => x === p || chosen.includes(x)));
    return (
        <div className="lines ai-perms">
            {grantable.map(p => (
                <button type="button" key={p} className={chosen.includes(p) ? 'ai-perm ai-tick on' : 'ai-perm ai-tick'} role="checkbox" aria-checked={chosen.includes(p)} onClick={() => toggle(p)}>
                    <span className="ai-ck">{chosen.includes(p) && <Icon name="check" />}</span>
                    <Words permission={p} />
                    {asked?.includes(p) && <span className="ai-asked">{t('ai.consent.asks')}</span>}
                </button>
            ))}
            {off?.map(p => (
                <div className="ai-perm ai-off" key={p}>
                    <span className="ai-ck" />
                    <span><b>{permissionTitle(p)}</b><span className="sub">{t('ai.consent.notYours')}</span></span>
                </div>
            ))}
        </div>
    );
}
