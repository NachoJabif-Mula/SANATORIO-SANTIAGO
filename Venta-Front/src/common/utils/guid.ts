const GUID_REGEX = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** true si el valor tiene formato de GUID (id real persistido en base de datos). */
export const isGuid = (val: string | null | undefined): boolean =>
  val ? GUID_REGEX.test(val) : false;
