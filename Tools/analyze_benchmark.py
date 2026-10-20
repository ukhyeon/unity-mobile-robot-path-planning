"""BenchmarkResults/*.csv를 집계해 요약표와 그림을 만든다.

사용법: python3 Tools/analyze_benchmark.py [BenchmarkResults 경로]
출력: BenchmarkResults/summary.md, summary_planners.csv, summary_bugs.csv, reference.csv, figures/*.png
"""
import sys
from pathlib import Path

import matplotlib
import matplotlib.font_manager
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import dijkstra

ROOT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent / "BenchmarkResults"
FIG = ROOT / "figures"
FIG.mkdir(exist_ok=True)

PLANNERS = ["Astar", "Dijkstra", "RRT", "RRTstar"]
BUGS = ["Bug0", "Bug1", "Bug2"]
LABEL = {"Astar": "A*", "Dijkstra": "Dijkstra", "RRT": "RRT", "RRTstar": "RRT*",
         "Bug0": "Bug0", "Bug1": "Bug1", "Bug2": "Bug2"}
# 알고리즘마다 고정된 범주 색 (dataviz 기본 팔레트 순서, 패널별로 검증함)
COLOR = {"Astar": "#2a78d6", "Dijkstra": "#eb6834", "RRT": "#1baf7a", "RRTstar": "#eda100",
         "Bug0": "#e87ba4", "Bug1": "#008300", "Bug2": "#6250d6"}
INK, INK2, GRID, SURFACE = "#0b0b0b", "#52514e", "#e4e3df", "#fcfcfb"

for f in ("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",):
    if Path(f).exists():
        matplotlib.font_manager.fontManager.addfont(f)
plt.rcParams.update({
    "font.family": ["Noto Sans CJK JP", "DejaVu Sans"],  # CJK 통합 폰트라 한글 글리프 포함
    "axes.edgecolor": INK2, "axes.labelcolor": INK2, "xtick.color": INK2, "ytick.color": INK2,
    "axes.grid": True, "grid.color": GRID, "grid.linewidth": 0.8, "axes.axisbelow": True,
    "axes.spines.top": False, "axes.spines.right": False,
    "figure.facecolor": SURFACE, "axes.facecolor": SURFACE, "savefig.facecolor": SURFACE,
    "font.size": 10,
})

# ---------------- 기준 최단거리 ----------------

def load_occupancy():
    lines = [l.strip() for l in (ROOT / "occupancy.txt").read_text().splitlines() if l and not l.startswith("#")]
    occ = np.array([[c == "1" for c in l] for l in lines])  # occ[z, x]
    return occ, 0.25, -50.0


def reference_lengths(pairs, occ, res, origin):
    """0.25 격자 8-연결 최단경로 + 가시선 단축(string pulling). 점 로봇 기준 기하학적 최단거리 근사."""
    n = occ.shape[0]
    idx = np.arange(n * n).reshape(n, n)
    free = ~occ
    rows, cols, w = [], [], []
    zs, xs = np.nonzero(free)
    for dz, dx in [(0, 1), (1, 0), (1, 1), (1, -1)]:
        z1, x1 = zs + dz, xs + dx
        inb = (z1 < n) & (x1 >= 0) & (x1 < n)
        z0, x0, z1, x1 = zs[inb], xs[inb], z1[inb], x1[inb]
        ok = free[z1, x1]
        if dz and dx:  # 대각선은 양옆이 모두 비어 있을 때만 (모서리 관통 방지)
            ok &= free[z0 + dz, x0] & free[z0, x0 + dx]
        z0, x0, z1, x1 = z0[ok], x0[ok], z1[ok], x1[ok]
        rows.append(idx[z0, x0]); cols.append(idx[z1, x1])
        w.append(np.full(len(z0), res * (np.sqrt(2) if dz and dx else 1.0)))
    r, c, ww = np.concatenate(rows), np.concatenate(cols), np.concatenate(w)
    graph = coo_matrix((np.concatenate([ww, ww]), (np.concatenate([r, c]), np.concatenate([c, r]))), shape=(n * n, n * n)).tocsr()

    def cell(x, z):
        return int((z - origin) / res), int((x - origin) / res)

    def los(p, q):
        d = np.hypot(*(q - p))
        for t in np.linspace(0, 1, max(2, int(d / 0.05))):
            x, z = p + (q - p) * t
            zi, xi = cell(x, z)
            if occ[zi, xi]:
                return False
        return True

    out = []
    for _, row in pairs.iterrows():
        s, g = np.array([row.sx, row.sz]), np.array([row.gx, row.gz])
        si, gi = idx[cell(*s)], idx[cell(*g)]
        dist, pred = dijkstra(graph, indices=si, return_predecessors=True)
        chain, k = [], gi
        while k != si and k >= 0:
            chain.append(k); k = pred[k]
        chain.append(si)
        pts = [s] + [np.array([origin + (k % n + 0.5) * res, origin + (k // n + 0.5) * res]) for k in chain[::-1][1:-1]] + [g]
        # 가시선 단축: 현재 점에서 보이는 가장 먼 점으로 건너뛴다
        short, i = [pts[0]], 0
        while i < len(pts) - 1:
            j = len(pts) - 1
            while j > i + 1 and not los(pts[i], pts[j]):
                j -= 1
            short.append(pts[j]); i = j
        length = sum(np.hypot(*(b - a)) for a, b in zip(short, short[1:]))
        out.append({"pair": int(row.pair), "ref_len": length, "grid_len": dist[gi]})
    return pd.DataFrame(out)

# ---------------- 집계 ----------------

pairs = pd.read_csv(ROOT / "pairs.csv")
occ, res, origin = load_occupancy()
ref_path = ROOT / "reference.csv"
if ref_path.exists():
    ref = pd.read_csv(ref_path)
else:
    ref = reference_lengths(pairs, occ, res, origin)
    ref.to_csv(ref_path, index=False)

pl = pd.read_csv(ROOT / "planner_trials.csv").merge(ref, on="pair")
bg = pd.read_csv(ROOT / "bug_trials.csv").merge(ref, on="pair")
pl["ratio"] = pl.path_len / pl.ref_len
bg["ratio"] = bg.travel_len / bg.ref_len
pl["valid"] = (pl.success == 1) & (pl.collision_len <= 0)
bg["valid"] = (bg.success == 1) & (bg.collision_len <= 0)


def q(s, p):
    return s.quantile(p) if len(s) else np.nan


rows = []
for a in PLANNERS:
    d = pl[pl.algo == a]; ok = d[d.success == 1]
    rows.append({
        "algo": LABEL[a], "trials": len(d),
        "success_%": 100 * d.success.mean(),
        "collision_free_%": 100 * d.valid.mean(),
        "retrace_hang": int(d.retrace_aborted.sum()),
        "iter_limit_fail": int(((d.success == 0) & (d.retrace_aborted == 0)).sum()),
        "plan_ms_median": d.plan_ms.median(), "plan_ms_p95": q(d.plan_ms, .95),
        "nodes_mean": d.nodes.mean(),
        "len_ratio_mean": ok.ratio.mean(), "len_ratio_std": ok.ratio.std(),
        "turn_deg_mean": ok.turn_deg.mean(),
        "min_clear_mean": ok.min_clear.mean(),
        "collision_len_mean": ok.collision_len.mean(),
    })
sp = pd.DataFrame(rows)
sp.to_csv(ROOT / "summary_planners.csv", index=False)

rows = []
for a in BUGS:
    d = bg[bg.algo == a]; ok = d[d.success == 1]
    rows.append({
        "algo": a, "trials": len(d),
        "success_%": 100 * d.success.mean(),
        "collision_free_%": 100 * d.valid.mean(),
        "time_s_median": ok.sim_time.median(),
        "len_ratio_mean": ok.ratio.mean(), "len_ratio_std": ok.ratio.std(),
        "turn_deg_mean": ok.turn_deg.mean(),
        "min_clear_mean": ok.min_clear.mean(),
        "collision_len_mean": ok.collision_len.mean(),
    })
sb = pd.DataFrame(rows)
sb.to_csv(ROOT / "summary_bugs.csv", index=False)

# ---------------- 그림 ----------------

paths = pd.read_csv(ROOT / "paths.csv")
p0 = pairs.iloc[0]


def draw_map(ax):
    ax.imshow(occ, origin="lower", extent=(-50, 50, -50, 50), cmap=matplotlib.colors.ListedColormap([SURFACE, "#9a9893"]),
              interpolation="nearest", zorder=0)
    ax.set_xlim(-50, 50); ax.set_ylim(-50, 50); ax.set_aspect("equal")
    ax.grid(False)
    ax.set_xlabel("x (m)"); ax.set_ylabel("z (m)")
    ax.plot(p0.sx, p0.sz, "o", ms=9, color=INK, mec=SURFACE, mew=2, zorder=5)
    ax.plot(p0.gx, p0.gz, "*", ms=14, color=INK, mec=SURFACE, mew=1.5, zorder=5)
    ax.annotate("시작", (p0.sx, p0.sz), xytext=(6, -12), textcoords="offset points", color=INK, fontsize=9)
    ax.annotate("목표", (p0.gx, p0.gz), xytext=(-14, -16), textcoords="offset points", color=INK, fontsize=9, bbox=dict(fc=SURFACE, ec="none", pad=1))


fig, axes = plt.subplots(1, 2, figsize=(12, 6.2))
for ax, algos, title in [(axes[0], PLANNERS, "전역 계획기 (RRT 계열은 seed 0)"), (axes[1], BUGS, "Bug 계열 (실제 주행 궤적)")]:
    draw_map(ax)
    for a in algos:
        d = paths[(paths.algo == a) & (paths.seed == 0)]
        if len(d):
            ax.plot(d.x.to_numpy(), d.z.to_numpy(), color=COLOR[a], lw=2, label=LABEL[a], zorder=3, solid_capstyle="round")
    ax.set_title(title, color=INK, loc="left", fontsize=11)
    ax.legend(loc="lower left", frameon=True, facecolor=SURFACE, edgecolor=GRID, framealpha=1, fontsize=9)
fig.suptitle("원본 시작·목표 쌍(pair 0)에서의 경로", color=INK, x=0.06, ha="left", fontsize=13)
fig.tight_layout()
fig.savefig(FIG / "paths_pair0.png", dpi=160, bbox_inches="tight")
plt.close(fig)


def boxplot(ax, data, labels, color):
    bp = ax.boxplot(data, widths=0.5, patch_artist=True, showfliers=True,
                    medianprops=dict(color=INK, lw=1.5), whiskerprops=dict(color=INK2), capprops=dict(color=INK2),
                    flierprops=dict(marker="o", ms=3, mfc=INK2, mec="none", alpha=.5))
    for b in bp["boxes"]:
        b.set(facecolor=color, edgecolor=INK2, alpha=.85)
    ax.set_xticks(range(1, len(labels) + 1), labels)


# 경로 길이 비율 (성공 시행)
fig, ax = plt.subplots(figsize=(8, 4.2))
algos = PLANNERS + BUGS
data = [pl[(pl.algo == a) & (pl.success == 1)].ratio.values for a in PLANNERS] + \
       [bg[(bg.algo == a) & (bg.success == 1)].ratio.values for a in BUGS]
boxplot(ax, data, [LABEL[a] for a in algos], "#86b6ef")
ax.axhline(1.0, color=INK2, lw=1, ls="--")
ax.annotate("기준 최단거리 = 1.0", (0.55, 1.0), xytext=(0, -12), textcoords="offset points", ha="left", color=INK2, fontsize=8)
ax.set_ylabel("경로 길이 / 기준 최단거리")
ax.set_title("경로 길이 비율 (성공한 시행만)", color=INK, loc="left")
fig.tight_layout(); fig.savefig(FIG / "length_ratio.png", dpi=160); plt.close(fig)

# 계획 시간 (전역 계획기)
fig, ax = plt.subplots(figsize=(6.5, 4.2))
boxplot(ax, [pl[pl.algo == a].plan_ms.values for a in PLANNERS], [LABEL[a] for a in PLANNERS], "#86b6ef")
ax.set_yscale("log")
ax.set_ylabel("계획 시간 (ms, 로그 축)")
ax.set_title("계획 시간 (전체 시행)", color=INK, loc="left")
fig.tight_layout(); fig.savefig(FIG / "plan_time.png", dpi=160); plt.close(fig)

# 성공률 vs 충돌 없는 성공률
fig, ax = plt.subplots(figsize=(8, 4.2))
succ = list(sp["success_%"]) + list(sb["success_%"])
valid = list(sp["collision_free_%"]) + list(sb["collision_free_%"])
x = np.arange(len(algos))
ax.bar(x, succ, width=0.6, color="#cde2fb", edgecolor="none", label="성공 (목표 도달)")
ax.bar(x, valid, width=0.36, color="#2a78d6", edgecolor="none", label="충돌 없이 성공")
for xi, s, v in zip(x, succ, valid):
    ax.annotate(f"{v:.0f}%", (xi, v), xytext=(0, 3), textcoords="offset points", ha="center", color=INK, fontsize=8)
    if s - v >= 6:
        ax.annotate(f"{s:.0f}%", (xi, s), xytext=(0, 3), textcoords="offset points", ha="center", color=INK2, fontsize=8)
ax.set_xticks(x, [LABEL[a] for a in algos])
ax.set_ylim(0, 112); ax.set_ylabel("비율 (%)")
ax.legend(loc="upper right", frameon=False, fontsize=9, ncol=2, bbox_to_anchor=(1, 1.02))
ax.set_title("성공률과 충돌 없는 성공률", color=INK, loc="left")
fig.tight_layout(); fig.savefig(FIG / "success.png", dpi=160); plt.close(fig)

# ---------------- 마크다운 요약 ----------------

def fmt(v, d=2):
    return "-" if pd.isna(v) else f"{v:.{d}f}"

md = ["# 경로계획 알고리즘 정량 비교 요약", "",
      f"- 시작·목표 쌍: {len(pairs)}개 (원본 1 + 무작위 {len(pairs) - 1}), 기준 최단거리 평균 {ref.ref_len.mean():.1f} m", ""]
md += ["## 전역 계획기", "",
       "| 알고리즘 | 시행 | 성공률 | 충돌 없는 성공률 | 실패: 무한루프 / 반복한도 | 계획 시간 중앙값 (ms) | p95 (ms) | 탐색 노드 | 길이 비율 | 회전량 (°) | 최소 이격 (m) |",
       "|---|---|---|---|---|---|---|---|---|---|---|"]
for _, r in sp.iterrows():
    md.append(f"| {r.algo} | {r.trials} | {fmt(r['success_%'],1)}% | {fmt(r['collision_free_%'],1)}% | {r.retrace_hang} / {r.iter_limit_fail} | {fmt(r.plan_ms_median,3)} | {fmt(r.plan_ms_p95,3)} | "
              f"{fmt(r.nodes_mean,0)} | {fmt(r.len_ratio_mean,3)} ± {fmt(r.len_ratio_std,3)} | {fmt(r.turn_deg_mean,0)} | {fmt(r.min_clear_mean,2)} |")
md += ["", "## Bug 계열", "",
       "| 알고리즘 | 시행 | 성공률 | 충돌 없는 성공률 | 도달 시간 중앙값 (s) | 길이 비율 | 회전량 (°) | 최소 이격 (m) |",
       "|---|---|---|---|---|---|---|---|"]
for _, r in sb.iterrows():
    md.append(f"| {r.algo} | {r.trials} | {fmt(r['success_%'],1)}% | {fmt(r['collision_free_%'],1)}% | {fmt(r.time_s_median,2)} | "
              f"{fmt(r.len_ratio_mean,3)} ± {fmt(r.len_ratio_std,3)} | {fmt(r.turn_deg_mean,0)} | {fmt(r.min_clear_mean,2)} |")
(ROOT / "summary.md").write_text("\n".join(md) + "\n")
print("\n".join(md))
