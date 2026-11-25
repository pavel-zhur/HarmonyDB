import { icons, type IconName } from './icons';

/** An icon of the design's set (kit/icons.ts), in the text's colour. */
export function Icon({ name, className }: { name: IconName; className?: string }) {
    return <svg className={className ? `ic ${className}` : 'ic'} viewBox="0 0 24 24" aria-hidden="true" dangerouslySetInnerHTML={{ __html: icons[name] }} />;
}

export type { IconName };
