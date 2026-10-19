/** Validate backup data before replacing browser records. No experimental values are logged. */
export function validStore(value: unknown): boolean {
  const record = (item: unknown): item is Record<string, unknown> => typeof item === "object" && item !== null && !Array.isArray(item);
  const text = (item: unknown): item is string => typeof item === "string" && item.length <= 10000;
  const date = (item: unknown): boolean => text(item) && /^\d{4}-\d{2}-\d{2}$/.test(item) && Number.isFinite(Date.parse(item));
  if (!record(value) || value.version !== 2 || !Array.isArray(value.cages) || !Array.isArray(value.breedings) || !Array.isArray(value.logs)) return false;
  if (value.cages.length > 10000 || value.breedings.length > 10000 || value.logs.length > 100000) return false;
  const ids = new Set<string>();
  for (const cage of value.cages) {
    if (!record(cage) || !text(cage.id) || !cage.id || ids.has(cage.id) || !text(cage.cageNo) || !date(cage.createdAt) || !text(cage.notes) ||
      !["在养", "已合笼", "已分笼", "已结束"].includes(String(cage.status)) || !Array.isArray(cage.groups) || !Array.isArray(cage.sourceCageIds) || !cage.sourceCageIds.every(text)) return false;
    ids.add(cage.id);
    for (const group of cage.groups) {
      if (!record(group) || !text(group.id) || !date(group.birthDate) || !["雄", "雌"].includes(String(group.sex)) || !text(group.genotype) || !Number.isSafeInteger(group.quantity) || Number(group.quantity) < 0) return false;
    }
  }
  for (const breeding of value.breedings) {
    if (!record(breeding) || !text(breeding.id) || !text(breeding.maleCageId) || !text(breeding.femaleCageId) || !date(breeding.pairedAt) || !text(breeding.notes) ||
      !["配笼中", "已确认怀孕", "已产仔", "已断奶", "未孕"].includes(String(breeding.status)) || !Array.isArray(breeding.offspringCageIds) || !breeding.offspringCageIds.every(text)) return false;
    for (const key of ["confirmedAt", "birthDate", "weanedAt"]) if (breeding[key] && !date(breeding[key])) return false;
    if (breeding.pups !== undefined && (!Number.isSafeInteger(breeding.pups) || Number(breeding.pups) < 0)) return false;
  }
  return value.logs.every(item => record(item) && text(item.id) && text(item.at) && Number.isFinite(Date.parse(item.at)) && text(item.type) && text(item.title) && text(item.detail));
}
