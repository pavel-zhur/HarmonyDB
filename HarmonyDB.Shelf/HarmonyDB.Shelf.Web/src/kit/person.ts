import { i18n } from '../i18n';

/** A person as shown by name: their name; one whose profile is deleted (the API gives no name then), so worded. */
export function personName(person: { name: string; gone?: boolean }): string {
    return person.gone ? i18n.t('common.gone') : person.name;
}
