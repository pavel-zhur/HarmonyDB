import { jsonEnum } from '../dotnet/json/JsonSerializer';

export enum NoteAlteration {
    Flat = 0,
    Sharp = 1,
}

export const NoteAlterationJson = jsonEnum<NoteAlteration>('HarmonyDB.Common.NoteAlteration');
