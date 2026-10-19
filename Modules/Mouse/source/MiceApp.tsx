"use client";

import React, { FormEvent, useEffect, useMemo, useState } from "react";
import { validStore } from "./validation";
import importedStoreData from "./importedStore.json";

type Sex = "雄" | "雌";
type CageStatus = "在养" | "已合笼" | "已分笼" | "已结束";
type BreedStatus = "配笼中" | "已确认怀孕" | "已产仔" | "已断奶" | "未孕";

type MouseGroup = {
  id: string;
  birthDate: string;
  sex: Sex;
  genotype: string;
  quantity: number;
};

type Cage = {
  id: string;
  cageNo: string;
  createdAt: string;
  status: CageStatus;
  groups: MouseGroup[];
  notes: string;
  sourceCageIds: string[];
  breedingId?: string;
};

type Breeding = {
  id: string;
  maleCageId: string;
  femaleCageId: string;
  pairedAt: string;
  status: BreedStatus;
  confirmedAt?: string;
  birthDate?: string;
  weanedAt?: string;
  pups?: number;
  offspringCageIds: string[];
  notes: string;
};

type Log = {
  id: string;
  at: string;
  type: "新建" | "合笼" | "分笼" | "繁育" | "更新" | "备份";
  title: string;
  detail: string;
};

type Store = { version: number; cages: Cage[]; breedings: Breeding[]; logs: Log[] };
type Tab = "home" | "cages" | "breeding" | "logs" | "settings";
type Modal = "new" | "merge" | "split" | "breed" | "progress" | "detail" | null;

const STORAGE_KEY = "fluolab-mice-v1";
const genotypes = ["WT", "Shank3-KO（HO）", "Shank3-KO（杂合）", "Shank3-KO（待鉴定）"];
const today = () => new Date().toISOString().slice(0, 10);
const uid = (prefix: string) => `${prefix}_${Date.now()}_${Math.random().toString(36).slice(2, 7)}`;
const fmtDate = (date?: string) => date ? date.replace(/-/g, ".") : "—";
const cageCount = (cage: Cage) => cage.groups.reduce((sum, group) => sum + group.quantity, 0);
const ageLabel = (birthDate: string) => {
  const birth = new Date(`${birthDate}T00:00:00`);
  const now = new Date();
  now.setHours(0, 0, 0, 0);
  const days = Math.max(0, Math.floor((now.getTime() - birth.getTime()) / 86400000));
  if (days < 7) return `${days} 天`;
  const weeks = Math.floor(days / 7);
  return days % 7 ? `${weeks} 周 + ${days % 7} 天` : `${weeks} 周`;
};
const addDays = (date: string, days: number) => {
  const value = new Date(`${date}T00:00:00`);
  value.setDate(value.getDate() + days);
  return value.toISOString().slice(0, 10);
};

const seedStore = importedStoreData as Store;

const blankGroup = (): MouseGroup => ({ id: uid("group"), birthDate: today(), sex: "雄", genotype: "WT", quantity: 1 });
const cloneSeed = (): Store => JSON.parse(JSON.stringify(seedStore));

function groupSummary(groups: MouseGroup[]) {
  return groups.map((g) => `${g.sex} · ${g.genotype} × ${g.quantity}`).join("；");
}

function ModalShell({ title, eyebrow, onClose, children, wide = false }: { title: string; eyebrow?: string; onClose: () => void; children: React.ReactNode; wide?: boolean }) {
  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <section className={`modal-card ${wide ? "modal-wide" : ""}`} role="dialog" aria-modal="true" aria-label={title}>
        <div className="modal-head">
          <div>{eyebrow && <p className="eyebrow">{eyebrow}</p>}<h2>{title}</h2></div>
          <button className="icon-button" onClick={onClose} aria-label="关闭">×</button>
        </div>
        {children}
      </section>
    </div>
  );
}

export default function MiceApp() {
  const [store, setStore] = useState<Store>(cloneSeed);
  const [loaded, setLoaded] = useState(false);
  const [tab, setTab] = useState<Tab>("home");
  const [modal, setModal] = useState<Modal>(null);
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState("全部");
  const [selectedCageId, setSelectedCageId] = useState<string>("");
  const [toast, setToast] = useState("");

  useEffect(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved) {
        const parsed: unknown = JSON.parse(saved);
        if (!validStore(parsed)) throw new Error("invalid local store");
        setStore(parsed as Store);
      }
    } catch { alert("本机记录无法读取，已保留原始内容且暂停自动保存。请导出原始记录并核对备份，不要清理浏览器数据。"); return; }
    setLoaded(true);
  }, []);

  useEffect(() => {
    if (loaded) {
      try { localStorage.setItem(STORAGE_KEY, JSON.stringify(store)); }
      catch { setLoaded(false); alert("浏览器存储不可写，后续修改未自动保存，请立即导出备份。"); }
    }
  }, [store, loaded]);

  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(""), 2600);
    return () => window.clearTimeout(timer);
  }, [toast]);

  const activeCages = useMemo(() => store.cages.filter((cage) => cage.status === "在养"), [store.cages]);
  const totalMice = useMemo(() => activeCages.reduce((sum, cage) => sum + cageCount(cage), 0), [activeCages]);
  const sexTotals = useMemo(() => activeCages.flatMap((cage) => cage.groups).reduce((totals, group) => {
    totals[group.sex] += group.quantity;
    return totals;
  }, { 雄: 0, 雌: 0 } as Record<Sex, number>), [activeCages]);
  const genotypeSummary = useMemo(() => {
    const totals = activeCages.flatMap((cage) => cage.groups).reduce<Record<string, number>>((result, group) => {
      result[group.genotype] = (result[group.genotype] || 0) + group.quantity;
      return result;
    }, Object.create(null) as Record<string, number>);
    return Object.entries(totals).sort((a, b) => b[1] - a[1]).map(([name, count]) => `${name} ${count}`).join(" · ") || "暂无基因型数据";
  }, [activeCages]);
  const breedingMice = useMemo(() => store.breedings.filter((item) => ["配笼中", "已确认怀孕", "已产仔"].includes(item.status)).length, [store.breedings]);
  const selectedCage = store.cages.find((cage) => cage.id === selectedCageId);

  const visibleCages = useMemo(() => {
    const query = search.trim().toLowerCase();
    return store.cages.filter((cage) => {
      const matchesSearch = !query || `${cage.cageNo} ${cage.notes} ${groupSummary(cage.groups)}`.toLowerCase().includes(query);
      const matchesFilter = filter === "全部" || cage.status === filter || cage.groups.some((g) => g.genotype === filter || g.sex === filter);
      return matchesSearch && matchesFilter;
    });
  }, [store.cages, search, filter]);

  const makeCageNo = (date: string, cages = store.cages) => {
    const prefix = date.replace(/-/g, "");
    const serial = cages.filter((cage) => cage.cageNo.startsWith(prefix)).length + 1;
    return `${prefix}${String(serial).padStart(4, "0")}`;
  };

  const addLog = (type: Log["type"], title: string, detail: string): Log => ({ id: uid("log"), at: new Date().toISOString(), type, title, detail });
  const openDetail = (id: string) => { setSelectedCageId(id); setModal("detail"); };

  const navItems: { id: Tab; label: string; glyph: string }[] = [
    { id: "home", label: "概览", glyph: "⌂" },
    { id: "cages", label: "笼位", glyph: "▦" },
    { id: "breeding", label: "繁育", glyph: "◎" },
    { id: "logs", label: "日志", glyph: "≡" },
    { id: "settings", label: "数据", glyph: "◇" },
  ];
  const pageMeta: Record<Tab, { eyebrow: string; title: string; subtitle: string }> = {
    home: { eyebrow: "MOUSE COLONY", title: "鼠群概览", subtitle: "在养小鼠的笼位、基因型与繁育进度，集中查看。" },
    cages: { eyebrow: "COLONY", title: "笼位管理", subtitle: "笼号、数量、出生日期与基因型放在最醒目的位置。" },
    breeding: { eyebrow: "BREEDING", title: "繁育记录", subtitle: "从配笼到产仔，父母来源、进度与子代关系持续衔接。" },
    logs: { eyebrow: "ACTIVITY", title: "操作日志", subtitle: "旧系统记录以及后续新建、合笼、分笼和繁育操作都按时间排列。" },
    settings: { eyebrow: "YOUR DATA", title: "数据管理", subtitle: "本机保存，随时导入和导出备份。" },
  };

  return (
    <main className="app-shell">
      <aside className="sidebar">
        <div className="brand"><span className="brand-mark">M</span><div><strong>鼠鼠管理</strong><small>LAB COLONY</small></div></div>
        <nav>{navItems.map((item) => <button key={item.id} className={tab === item.id ? "active" : ""} onClick={() => setTab(item.id)}><span>{item.glyph}</span>{item.label}</button>)}</nav>
        <div className="sidebar-foot"><span className="status-dot" /><div><strong>本机数据模式</strong><small>{loaded ? "修改自动保存于此浏览器" : "存储异常，修改未保存"}</small></div></div>
      </aside>

      <div className="workspace">
        {!loaded && <div role="alert" className="form-hint">正在检查本机存储或存储不可写。为保护记录，请先备份原始内容。<button onClick={() => { const raw = localStorage.getItem(STORAGE_KEY) || "{}"; const url = URL.createObjectURL(new Blob([raw], {type:"application/json"})); const link = document.createElement("a"); link.href = url; link.download = "FluoLab-原始存储备份.json"; link.click(); URL.revokeObjectURL(url); }}>导出原始存储内容</button></div>}
        <header className="topbar">
          <div className="hero-copy"><p className="eyebrow">{pageMeta[tab].eyebrow}</p><h1>{pageMeta[tab].title}</h1><p className="hero-subtitle">{pageMeta[tab].subtitle}</p></div>
          <button className="primary-button" onClick={() => setModal("new")}><span>＋</span> 新建笼位</button>
        </header>

        <section className="content page-enter" key={tab}>
          {tab === "home" && <HomeView activeCages={activeCages} totalMice={totalMice} maleCount={sexTotals.雄} femaleCount={sexTotals.雌} genotypeSummary={genotypeSummary} breedingCount={breedingMice} logs={store.logs} onDetail={openDetail} onAction={(next) => setModal(next)} onTab={setTab} />}
          {tab === "cages" && <CagesView cages={visibleCages} search={search} setSearch={setSearch} filter={filter} setFilter={setFilter} onDetail={openDetail} onMerge={() => setModal("merge")} onSplit={() => setModal("split")} />}
          {tab === "breeding" && <BreedingView records={store.breedings} cages={store.cages} onNew={() => setModal("breed")} onProgress={(id) => { setSelectedCageId(id); setModal("progress"); }} />}
          {tab === "logs" && <LogsView logs={store.logs} />}
          {tab === "settings" && <SettingsView store={store} onImport={(next) => { setStore({ ...next, version: 2 }); setLoaded(true); setToast("备份已导入"); }} onReset={() => { setStore(cloneSeed()); setLoaded(true); setToast("FluoLab 当前模块已清空"); }} onBackup={() => setStore((prev) => ({ ...prev, logs: [addLog("备份", "导出本机备份", `共 ${prev.cages.length} 条笼位记录`), ...prev.logs] }))} />}
        </section>
      </div>

      <nav className="mobile-nav">{navItems.map((item) => <button key={item.id} className={tab === item.id ? "active" : ""} onClick={() => setTab(item.id)}><span>{item.glyph}</span>{item.label}</button>)}</nav>

      {modal === "new" && <NewCageModal cages={store.cages} makeCageNo={makeCageNo} onClose={() => setModal(null)} onSave={(cage) => { setStore((prev) => ({ ...prev, cages: [cage, ...prev.cages], logs: [addLog("新建", `新增笼位 ${cage.cageNo}`, `${cageCount(cage)} 只 · ${groupSummary(cage.groups)}`), ...prev.logs] })); setModal(null); setToast(`已创建 ${cage.cageNo}`); }} />}
      {modal === "merge" && <MergeModal cages={activeCages} makeCageNo={makeCageNo} onClose={() => setModal(null)} onSave={(sourceIds, notes) => {
        const sources = store.cages.filter((cage) => sourceIds.includes(cage.id));
        const groups = sources.flatMap((cage) => cage.groups).reduce<MouseGroup[]>((list, group) => {
          const match = list.find((item) => item.birthDate === group.birthDate && item.sex === group.sex && item.genotype === group.genotype);
          if (match) match.quantity += group.quantity; else list.push({ ...group, id: uid("group") });
          return list;
        }, []);
        const createdAt = today(); const cageNo = makeCageNo(createdAt);
        const cage: Cage = { id: uid("cage"), cageNo, createdAt, status: "在养", groups, notes, sourceCageIds: sourceIds };
        setStore((prev) => ({ ...prev, cages: [cage, ...prev.cages.map((item) => sourceIds.includes(item.id) ? { ...item, status: "已合笼" as CageStatus } : item)], logs: [addLog("合笼", `合笼生成 ${cageNo}`, `来源：${sources.map((c) => c.cageNo).join("、")}；共 ${cageCount(cage)} 只`), ...prev.logs] }));
        setModal(null); setToast(`合笼完成，新笼号 ${cageNo}`);
      }} />}
      {modal === "split" && <SplitModal cages={activeCages} makeCageNo={makeCageNo} onClose={() => setModal(null)} onSave={(sourceId, outputs, notes) => {
        const source = store.cages.find((cage) => cage.id === sourceId)!;
        const createdAt = today(); const cageA: Cage = { id: uid("cage"), cageNo: makeCageNo(createdAt), createdAt, status: "在养", groups: outputs[0], notes, sourceCageIds: [sourceId] };
        const cageB: Cage = { id: uid("cage"), cageNo: makeCageNo(createdAt, [cageA, ...store.cages]), createdAt, status: "在养", groups: outputs[1], notes, sourceCageIds: [sourceId] };
        setStore((prev) => ({ ...prev, cages: [cageB, cageA, ...prev.cages.map((item) => item.id === sourceId ? { ...item, status: "已分笼" as CageStatus } : item)], logs: [addLog("分笼", `${source.cageNo} 完成分笼`, `新笼：${cageA.cageNo}（${cageCount(cageA)} 只）、${cageB.cageNo}（${cageCount(cageB)} 只）`), ...prev.logs] }));
        setModal(null); setToast("分笼完成，已生成两个新笼号");
      }} />}
      {modal === "breed" && <BreedModal cages={activeCages} onClose={() => setModal(null)} onSave={(record) => { setStore((prev) => ({ ...prev, breedings: [record, ...prev.breedings], logs: [addLog("繁育", "建立繁育组合", `${store.cages.find((c) => c.id === record.maleCageId)?.cageNo} × ${store.cages.find((c) => c.id === record.femaleCageId)?.cageNo}`), ...prev.logs] })); setModal(null); setToast("繁育组合已建立"); }} />}
      {modal === "progress" && <ProgressModal record={store.breedings.find((item) => item.id === selectedCageId)!} makeCageNo={makeCageNo} onClose={() => setModal(null)} onSave={(updated, offspring) => {
        setStore((prev) => ({ ...prev, breedings: prev.breedings.map((item) => item.id === updated.id ? { ...updated, offspringCageIds: offspring ? [...updated.offspringCageIds, offspring.id] : updated.offspringCageIds } : item), cages: offspring ? [offspring, ...prev.cages] : prev.cages, logs: [addLog("繁育", `繁育进度：${updated.status}`, offspring ? `断奶并生成子代笼 ${offspring.cageNo}` : `记录日期 ${updated.confirmedAt || updated.birthDate || updated.weanedAt}`), ...prev.logs] }));
        setModal(null); setToast(`已更新为“${updated.status}”`);
      }} />}
      {modal === "detail" && selectedCage && <CageDetail cage={selectedCage} allCages={store.cages} logs={store.logs} onClose={() => setModal(null)} />}
      {toast && <div className="toast" role="status"><span>✓</span>{toast}</div>}
    </main>
  );
}

function HomeView({ activeCages, totalMice, maleCount, femaleCount, genotypeSummary, breedingCount, logs, onDetail, onAction, onTab }: { activeCages: Cage[]; totalMice: number; maleCount: number; femaleCount: number; genotypeSummary: string; breedingCount: number; logs: Log[]; onDetail: (id: string) => void; onAction: (modal: Modal) => void; onTab: (tab: Tab) => void }) {
  return <>
    <div className="stat-grid">
      <article className="stat-card mint"><div><p>在养小鼠</p><strong>{totalMice}<small> 只</small></strong></div><span className="stat-symbol">↗</span><footer>雄 {maleCount} · 雌 {femaleCount} · 共 {activeCages.length} 笼</footer></article>
      <article className="stat-card"><div><p>当前笼位</p><strong>{activeCages.length}<small> 笼</small></strong></div><span className="stat-symbol">▦</span><footer>{genotypeSummary}</footer></article>
      <article className="stat-card"><div><p>进行中繁育</p><strong>{breedingCount}<small> 组</small></strong></div><span className="stat-symbol">◎</span><footer>{breedingCount ? "按计划持续观察" : "暂无进行中的组合"}</footer></article>
    </div>
    <div className="dashboard-grid">
      <section className="panel"><div className="panel-head"><div><p className="eyebrow">COLONY</p><h2>最近笼位</h2></div><button className="text-button" onClick={() => onTab("cages")}>查看全部 →</button></div><div className="cage-list compact">{activeCages.slice(0, 4).map((cage) => <CageRow key={cage.id} cage={cage} onClick={() => onDetail(cage.id)} />)}</div></section>
      <section className="panel action-panel"><div className="panel-head"><div><p className="eyebrow">QUICK ACTION</p><h2>常用操作</h2></div></div><div className="quick-grid"><button onClick={() => onAction("new")}><span>＋</span><strong>新建笼位</strong><small>自动生成笼号</small></button><button onClick={() => onAction("split")}><span>⑂</span><strong>分笼</strong><small>拆分并追踪来源</small></button><button onClick={() => onAction("merge")}><span>⇥</span><strong>合笼</strong><small>合并笼内小组</small></button><button onClick={() => onAction("breed")}><span>◎</span><strong>开始繁育</strong><small>关联父笼与母笼</small></button></div></section>
    </div>
    <section className="panel recent-log"><div className="panel-head"><div><p className="eyebrow">ACTIVITY</p><h2>最近动态</h2></div><button className="text-button" onClick={() => onTab("logs")}>完整日志 →</button></div><div className="timeline">{logs.slice(0, 3).map((log) => <LogRow key={log.id} log={log} />)}</div></section>
  </>;
}

function CagesView({ cages, search, setSearch, filter, setFilter, onDetail, onMerge, onSplit }: { cages: Cage[]; search: string; setSearch: (value: string) => void; filter: string; setFilter: (value: string) => void; onDetail: (id: string) => void; onMerge: () => void; onSplit: () => void }) {
  const filters = ["全部", "在养", "已结束", "雄", "雌", ...genotypes];
  return <section className="panel cage-panel"><div className="toolbar"><label className="search-box"><span>⌕</span><input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="搜索笼号、基因型或备注" /></label><div className="toolbar-actions"><button className="secondary-button" onClick={onMerge}>⇥ 合笼</button><button className="secondary-button" onClick={onSplit}>⑂ 分笼</button></div></div><div className="filter-row">{filters.map((item) => <button key={item} className={filter === item ? "active" : ""} onClick={() => setFilter(item)}>{item}</button>)}</div><div className="table-wrap"><table><thead><tr><th>笼号</th><th>笼内构成</th><th>出生日期 / 周龄</th><th>数量</th><th>状态</th><th /></tr></thead><tbody>{cages.map((cage) => <tr key={cage.id} onClick={() => onDetail(cage.id)}><td><strong>{cage.cageNo}</strong><small>{fmtDate(cage.createdAt)} 建笼</small></td><td><div className="tags">{cage.groups.slice(0, 3).map((g) => <span key={g.id} className={g.sex === "雄" ? "tag blue" : "tag coral"}>{g.sex} · {g.genotype} × {g.quantity}</span>)}</div></td><td>{[...new Map(cage.groups.map((g) => [g.birthDate, `${fmtDate(g.birthDate)}（${ageLabel(g.birthDate)}）`])).values()].join(" / ")}</td><td><strong className="count">{cageCount(cage)}</strong> 只</td><td><span className={`status ${cage.status === "在养" ? "live" : "muted"}`}>{cage.status}</span></td><td><button className="row-arrow" aria-label="查看详情">›</button></td></tr>)}</tbody></table>{!cages.length && <div className="empty"><span>⌕</span><strong>没有找到匹配笼位</strong><p>试试更换搜索词或筛选条件</p></div>}</div></section>;
}

function CageRow({ cage, onClick }: { cage: Cage; onClick: () => void }) {
  return <button className="cage-row" onClick={onClick}><span className="cage-icon">▦</span><div className="cage-main"><strong>{cage.cageNo}</strong><small>{groupSummary(cage.groups)}</small></div><div className="cage-total"><strong>{cageCount(cage)}</strong><small>只</small></div><span className="row-arrow">›</span></button>;
}

function BreedingView({ records, cages, onNew, onProgress }: { records: Breeding[]; cages: Cage[]; onNew: () => void; onProgress: (id: string) => void }) {
  const cageNo = (id: string) => cages.find((cage) => cage.id === id)?.cageNo || "来源笼已归档";
  const stages: BreedStatus[] = ["配笼中", "已确认怀孕", "已产仔", "已断奶"];
  return <><div className="section-intro"><div><p>以父笼和母笼建立繁育关系，子代断奶后自动生成新笼。</p></div><button className="primary-button" onClick={onNew}>＋ 建立繁育组合</button></div><div className="breeding-grid">{records.map((record) => { const isFailed = record.status === "未孕"; const stageIndex = stages.indexOf(record.status); return <article className={`breed-card ${isFailed ? "breed-closed" : ""}`} key={record.id}><header><span className="breed-badge">{record.status}</span><small>配笼 {fmtDate(record.pairedAt)} · 预计出生 {fmtDate(addDays(record.pairedAt, 20))}</small></header><div className="parent-pair"><div><span className="sex-symbol male">♂</span><small>父笼</small><strong>{cageNo(record.maleCageId)}</strong></div><span className="pair-line">×</span><div><span className="sex-symbol female">♀</span><small>母笼</small><strong>{cageNo(record.femaleCageId)}</strong></div></div>{isFailed ? <p className="closed-note">本次未孕，记录已归档</p> : <div className="stage-line">{stages.map((stage, index) => <div key={stage} className={index <= stageIndex ? "done" : ""}><span>{index < stageIndex ? "✓" : index + 1}</span><small>{stage}</small></div>)}</div>}{record.notes && <p className="card-note">{record.notes}</p>}<footer><div>{record.pups ? <><strong>{record.pups}</strong><small> 只产仔</small></> : <small>{isFailed ? "无子代" : "尚未记录产仔数量"}</small>}</div>{!isFailed && record.status !== "已断奶" && <button className="secondary-button" onClick={() => onProgress(record.id)}>更新进度 →</button>}</footer></article>; })}</div></>;
}

function LogsView({ logs }: { logs: Log[] }) {
  const [type, setType] = useState("全部");
  const shown = type === "全部" ? logs : logs.filter((log) => log.type === type);
  return <section className="panel log-panel"><div className="filter-row">{["全部", "新建", "合笼", "分笼", "繁育", "更新", "备份"].map((item) => <button key={item} className={type === item ? "active" : ""} onClick={() => setType(item)}>{item}</button>)}</div><div className="timeline full">{shown.map((log) => <LogRow key={log.id} log={log} />)}</div></section>;
}

function LogRow({ log }: { log: Log }) {
  const date = new Date(log.at);
  return <div className="log-row"><span className={`log-dot type-${log.type}`} /><div><strong>{log.title}</strong><p>{log.detail}</p></div><time>{date.toLocaleDateString("zh-CN", { month: "2-digit", day: "2-digit" })}<small>{date.toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit" })}</small></time></div>;
}

function SettingsView({ store, onImport, onReset, onBackup }: { store: Store; onImport: (store: Store) => void; onReset: () => void; onBackup: () => void }) {
  const exportData = () => { const blob = new Blob([JSON.stringify(store, null, 2)], { type: "application/json" }); const link = document.createElement("a"); link.href = URL.createObjectURL(blob); link.download = `鼠鼠管理备份-${today()}.json`; link.click(); URL.revokeObjectURL(link.href); onBackup(); };
  const importData = (file?: File) => { if (!file) return; if (file.size > 5 * 1024 * 1024) { alert("备份超过 5 MB，请先核对文件。"); return; } const reader = new FileReader(); reader.onload = () => { try { const parsed: unknown = JSON.parse(String(reader.result)); if (!validStore(parsed)) throw new Error(); if (!confirm("导入将替换 FluoLab 小鼠模块当前记录。请先导出备份。确定继续吗？")) return; onImport(parsed as Store); } catch { alert("备份格式或字段无效，当前记录未修改。"); } }; reader.onerror = () => alert("备份读取失败，当前记录未修改。"); reader.readAsText(file); };
  return <div className="settings-grid"><section className="panel setting-card"><span className="setting-icon">⇩</span><div><h2>导出备份</h2><p>把全部笼位、繁育和日志保存为一个文件。更换浏览器或清理数据前务必备份。</p><button className="primary-button" onClick={exportData}>导出本机数据</button></div></section><section className="panel setting-card"><span className="setting-icon">⇧</span><div><h2>导入备份</h2><p>可导入原鼠鼠管理导出的 JSON。导入前校验字段，并明确确认替换，不自动迁移旧记录。</p><label className="secondary-button file-button">选择备份文件<input type="file" accept="application/json" onChange={(e) => importData(e.target.files?.[0])} /></label></div></section><section className="panel setting-card danger-zone"><span className="setting-icon">↻</span><div><h2>清空当前模块</h2><p>仅清空 FluoLab 此浏览器中的记录，不改动原鼠鼠管理。</p><button className="danger-button" onClick={() => confirm("确定清空 FluoLab 当前小鼠记录？请先导出备份。") && onReset()}>清空为空库</button></div></section><section className="panel about-card"><p className="eyebrow">FLUOLAB · LOCAL</p><h2>独立本机数据空间</h2><p>初次使用为空库。数据只保存在当前浏览器的固定本地地址中，不上传、不自动读取原系统记录。请定期备份 JSON。繁育预计日期仅作原系统日程提示，须按实验事实核对。</p><div><span>整笼管理</span><span>混合分组</span><span>操作留痕</span><span>谱系关联</span></div></section></div>;
}

function GroupEditor({ groups, setGroups, allowRemove = true }: { groups: MouseGroup[]; setGroups: (groups: MouseGroup[]) => void; allowRemove?: boolean }) {
  const update = (id: string, field: keyof MouseGroup, value: string | number) => setGroups(groups.map((group) => group.id === id ? { ...group, [field]: value } : group));
  return <div className="group-editor">{groups.map((group, index) => <div className="group-edit-row" key={group.id}><div className="group-index">{index + 1}</div><label><span>出生日期</span><input type="date" value={group.birthDate} onChange={(e) => update(group.id, "birthDate", e.target.value)} required /></label><label><span>性别</span><select value={group.sex} onChange={(e) => update(group.id, "sex", e.target.value)}><option>雄</option><option>雌</option></select></label><label><span>基因型</span><select value={group.genotype} onChange={(e) => update(group.id, "genotype", e.target.value)}>{genotypes.map((g) => <option key={g}>{g}</option>)}</select></label><label className="qty-field"><span>数量</span><input type="number" min="1" value={group.quantity} onChange={(e) => update(group.id, "quantity", Number(e.target.value))} required /></label>{allowRemove && groups.length > 1 && <button type="button" className="remove-button" onClick={() => setGroups(groups.filter((item) => item.id !== group.id))} aria-label="删除这个小组">×</button>}</div>)}<button type="button" className="add-group" onClick={() => setGroups([...groups, blankGroup()])}>＋ 添加另一组不同属性的小鼠</button></div>;
}

function NewCageModal({ cages, makeCageNo, onClose, onSave }: { cages: Cage[]; makeCageNo: (date: string, cages?: Cage[]) => string; onClose: () => void; onSave: (cage: Cage) => void }) {
  const [date, setDate] = useState(today()); const [notes, setNotes] = useState(""); const [groups, setGroups] = useState<MouseGroup[]>([blankGroup()]);
  const submit = (e: FormEvent) => { e.preventDefault(); onSave({ id: uid("cage"), cageNo: makeCageNo(date, cages), createdAt: date, status: "在养", groups, notes, sourceCageIds: [] }); };
  return <ModalShell title="新建笼位" eyebrow={`预览笼号 · ${makeCageNo(date, cages)}`} onClose={onClose} wide><form onSubmit={submit}><div className="form-section"><div className="section-label"><span>01</span><div><strong>建笼信息</strong><small>笼号将按建笼日期自动生成</small></div></div><div className="form-grid"><label><span>建笼日期</span><input type="date" value={date} onChange={(e) => setDate(e.target.value)} required /></label><label><span>备注（选填）</span><input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="例如：行为学实验备用" /></label></div></div><div className="form-section"><div className="section-label"><span>02</span><div><strong>笼内小鼠</strong><small>不同出生日期、性别或基因型请分组填写</small></div></div><GroupEditor groups={groups} setGroups={setGroups} /></div><div className="modal-actions"><button type="button" className="ghost-button" onClick={onClose}>取消</button><button className="primary-button">确认新建 · {groups.reduce((s, g) => s + g.quantity, 0)} 只</button></div></form></ModalShell>;
}

function MergeModal({ cages, makeCageNo, onClose, onSave }: { cages: Cage[]; makeCageNo: (date: string) => string; onClose: () => void; onSave: (ids: string[], notes: string) => void }) {
  const [ids, setIds] = useState<string[]>([]); const [notes, setNotes] = useState("");
  const toggle = (id: string) => setIds(ids.includes(id) ? ids.filter((item) => item !== id) : [...ids, id]);
  return <ModalShell title="合笼" eyebrow={`将生成新笼号 · ${makeCageNo(today())}`} onClose={onClose} wide><form onSubmit={(e) => { e.preventDefault(); if (ids.length < 2) return; onSave(ids, notes); }}><p className="form-hint">选择至少两个来源笼。完成后来源笼会归档，新笼保留全部来源关系。</p><div className="select-cages">{cages.map((cage) => <label key={cage.id} className={ids.includes(cage.id) ? "selected" : ""}><input type="checkbox" checked={ids.includes(cage.id)} onChange={() => toggle(cage.id)} /><span className="checkmark">✓</span><div><strong>{cage.cageNo}</strong><small>{groupSummary(cage.groups)}</small></div><b>{cageCount(cage)} 只</b></label>)}</div><label className="full-label"><span>合笼备注（选填）</span><input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="例如：繁育配笼" /></label><div className="merge-summary"><span>已选择 {ids.length} 笼</span><strong>合计 {cages.filter((c) => ids.includes(c.id)).reduce((s, c) => s + cageCount(c), 0)} 只</strong></div><div className="modal-actions"><button type="button" className="ghost-button" onClick={onClose}>取消</button><button className="primary-button" disabled={ids.length < 2}>确认合笼</button></div></form></ModalShell>;
}

function SplitModal({ cages, makeCageNo, onClose, onSave }: { cages: Cage[]; makeCageNo: (date: string, cages?: Cage[]) => string; onClose: () => void; onSave: (id: string, groups: [MouseGroup[], MouseGroup[]], notes: string) => void }) {
  const [sourceId, setSourceId] = useState(cages[0]?.id || ""); const [alloc, setAlloc] = useState<Record<string, [number, number]>>({}); const [notes, setNotes] = useState("");
  const source = cages.find((cage) => cage.id === sourceId);
  useEffect(() => { if (!source) return; const next: Record<string, [number, number]> = {}; source.groups.forEach((g) => { const first = Math.ceil(g.quantity / 2); next[g.id] = [first, g.quantity - first]; }); setAlloc(next); }, [sourceId]);
  const valid = !!source && source.groups.every((g) => (alloc[g.id]?.[0] || 0) + (alloc[g.id]?.[1] || 0) === g.quantity) && Object.values(alloc).reduce((s, p) => s + p[0], 0) > 0 && Object.values(alloc).reduce((s, p) => s + p[1], 0) > 0;
  const submit = (e: FormEvent) => { e.preventDefault(); if (!source || !valid) return; const outputs = [0, 1].map((side) => source.groups.filter((g) => alloc[g.id][side] > 0).map((g) => ({ ...g, id: uid("group"), quantity: alloc[g.id][side] }))) as [MouseGroup[], MouseGroup[]]; onSave(source.id, outputs, notes); };
  return <ModalShell title="分笼" eyebrow="一个来源笼 · 两个新笼" onClose={onClose} wide><form onSubmit={submit}><label className="full-label"><span>选择来源笼</span><select value={sourceId} onChange={(e) => setSourceId(e.target.value)}>{cages.map((cage) => <option value={cage.id} key={cage.id}>{cage.cageNo} · {cageCount(cage)} 只</option>)}</select></label>{source && <><div className="split-head"><span>笼内小组</span><span>新笼 A · {makeCageNo(today())}</span><span>新笼 B · {makeCageNo(today(), [{ ...source, cageNo: makeCageNo(today()) }])}</span></div><div className="split-rows">{source.groups.map((group) => <div key={group.id}><div><strong>{group.sex} · {group.genotype}</strong><small>{fmtDate(group.birthDate)} · 共 {group.quantity} 只</small></div><input type="number" min="0" max={group.quantity} value={alloc[group.id]?.[0] ?? 0} onChange={(e) => setAlloc({ ...alloc, [group.id]: [Number(e.target.value), alloc[group.id]?.[1] || 0] })} /><input type="number" min="0" max={group.quantity} value={alloc[group.id]?.[1] ?? 0} onChange={(e) => setAlloc({ ...alloc, [group.id]: [alloc[group.id]?.[0] || 0, Number(e.target.value)] })} /></div>)}</div>{!valid && <p className="validation">每组分配数量之和必须等于原数量，且两个新笼都不能为空。</p>}</>}<label className="full-label"><span>分笼备注（选填）</span><input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="例如：按基因型分笼" /></label><div className="modal-actions"><button type="button" className="ghost-button" onClick={onClose}>取消</button><button className="primary-button" disabled={!valid}>确认分笼</button></div></form></ModalShell>;
}

function BreedModal({ cages, onClose, onSave }: { cages: Cage[]; onClose: () => void; onSave: (record: Breeding) => void }) {
  const male = cages.filter((c) => c.groups.some((g) => g.sex === "雄")); const female = cages.filter((c) => c.groups.some((g) => g.sex === "雌"));
  const [maleId, setMaleId] = useState(male[0]?.id || ""); const [femaleId, setFemaleId] = useState(female[0]?.id || ""); const [date, setDate] = useState(today()); const [notes, setNotes] = useState("");
  return <ModalShell title="建立繁育组合" eyebrow="关联父笼与母笼" onClose={onClose}><form onSubmit={(e) => { e.preventDefault(); onSave({ id: uid("breed"), maleCageId: maleId, femaleCageId: femaleId, pairedAt: date, status: "配笼中", offspringCageIds: [], notes }); }}><div className="parent-fields"><label><span className="sex-symbol male">♂</span><b>父笼</b><select value={maleId} onChange={(e) => setMaleId(e.target.value)}>{male.map((cage) => <option value={cage.id} key={cage.id}>{cage.cageNo} · {groupSummary(cage.groups.filter((g) => g.sex === "雄"))}</option>)}</select></label><div className="pair-line">×</div><label><span className="sex-symbol female">♀</span><b>母笼</b><select value={femaleId} onChange={(e) => setFemaleId(e.target.value)}>{female.map((cage) => <option value={cage.id} key={cage.id}>{cage.cageNo} · {groupSummary(cage.groups.filter((g) => g.sex === "雌"))}</option>)}</select></label></div><div className="form-grid"><label><span>配笼日期</span><input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></label><label><span>备注（选填）</span><input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="繁育目的或注意事项" /></label></div><p className="form-hint">第一版以父笼和母笼追踪谱系，断奶时将自动建立子代笼。</p><div className="modal-actions"><button type="button" className="ghost-button" onClick={onClose}>取消</button><button className="primary-button" disabled={!maleId || !femaleId}>确认建立</button></div></form></ModalShell>;
}

function ProgressModal({ record, makeCageNo, onClose, onSave }: { record: Breeding; makeCageNo: (date: string) => string; onClose: () => void; onSave: (record: Breeding, offspring?: Cage) => void }) {
  const order: BreedStatus[] = ["配笼中", "已确认怀孕", "已产仔", "已断奶"]; const next = order[order.indexOf(record.status) + 1];
  const [date, setDate] = useState(today()); const [pups, setPups] = useState(record.pups || 1); const [groups, setGroups] = useState<MouseGroup[]>([{ ...blankGroup(), birthDate: record.birthDate || today(), genotype: "待鉴定" }]);
  const submit = (e: FormEvent) => { e.preventDefault(); let updated = { ...record, status: next }; let offspring: Cage | undefined;
    if (next === "已确认怀孕") updated.confirmedAt = date;
    if (next === "已产仔") { updated.birthDate = date; updated.pups = pups; }
    if (next === "已断奶") { updated.weanedAt = date; offspring = { id: uid("cage"), cageNo: makeCageNo(date), createdAt: date, status: "在养", groups: groups.map((g) => ({ ...g, birthDate: record.birthDate || g.birthDate })), notes: `繁育子代 · ${record.notes || "无备注"}`, sourceCageIds: [record.maleCageId, record.femaleCageId], breedingId: record.id }; }
    onSave(updated, offspring);
  };
  return <ModalShell title={`更新为“${next}”`} eyebrow="繁育进度" onClose={onClose} wide={next === "已断奶"}><form onSubmit={submit}><div className="form-grid"><label><span>{next === "已确认怀孕" ? "确认日期" : next === "已产仔" ? "出生日期" : "断奶日期"}</span><input type="date" value={date} onChange={(e) => setDate(e.target.value)} required /></label>{next === "已产仔" && <label><span>产仔数量</span><input type="number" min="1" value={pups} onChange={(e) => setPups(Number(e.target.value))} /></label>}</div>{next === "已断奶" && <div className="form-section offspring"><div className="section-label"><span>＋</span><div><strong>登记断奶子代</strong><small>将生成新笼号 {makeCageNo(date)}</small></div></div><GroupEditor groups={groups} setGroups={setGroups} /></div>}<div className="modal-actions"><button type="button" className="ghost-button" onClick={onClose}>取消</button><button className="primary-button">保存进度</button></div></form></ModalShell>;
}

function CageDetail({ cage, allCages, onClose }: { cage: Cage; allCages: Cage[]; logs: Log[]; onClose: () => void }) {
  const sources = allCages.filter((item) => cage.sourceCageIds.includes(item.id));
  return <ModalShell title={cage.cageNo} eyebrow={`${cage.status} · 建笼 ${fmtDate(cage.createdAt)}`} onClose={onClose} wide><div className="detail-summary"><div><small>当前数量</small><strong>{cageCount(cage)}<span> 只</span></strong></div><div><small>笼内小组</small><strong>{cage.groups.length}<span> 组</span></strong></div><div><small>来源记录</small><strong>{sources.length || "—"}<span>{sources.length ? " 笼" : ""}</span></strong></div></div><div className="detail-section"><h3>笼内构成</h3>{cage.groups.map((group, index) => <div className="detail-group" key={group.id}><span>{String(index + 1).padStart(2, "0")}</span><div><strong>{group.sex} · {group.genotype}</strong><small>出生于 {fmtDate(group.birthDate)} · {ageLabel(group.birthDate)}</small></div><b>{group.quantity} 只</b></div>)}</div>{sources.length > 0 && <div className="detail-section"><h3>来源笼</h3><div className="source-chain">{sources.map((source) => <span key={source.id}>{source.cageNo}</span>)}<b>→</b><span className="current">{cage.cageNo}</span></div></div>}{cage.notes && <div className="detail-note"><small>备注</small><p>{cage.notes}</p></div>}<div className="modal-actions"><button className="primary-button" onClick={onClose}>完成</button></div></ModalShell>;
}
