# Summarises a baseline folder into markdown tables (docs/baselines/<name>/README.md reads these).
import re, glob, os, statistics as st, sys
root = sys.argv[1] if len(sys.argv) > 1 else '.'

def num(t, p):
    m = re.search(p, t)
    return float(m.group(1)) if m else float('nan')

print('### Duel bed — 20,000 fights a scenario, seed 1..20000\n')
print('| Scenario | Victory (never) | Deaths (never) | Charges/fight | Victory (losing:0.7) | Deaths (losing:0.7) | Escapes (losing:0.7) |')
print('|---|---|---|---|---|---|---|')
names = sorted({re.sub(r'-(never|losing07)\.txt$', '', os.path.basename(f)) for f in glob.glob(f'{root}/duel/*.txt')})
for n in names:
    a = open(f'{root}/duel/{n}-never.txt', encoding='utf-8', errors='replace').read()
    b = open(f'{root}/duel/{n}-losing07.txt', encoding='utf-8', errors='replace').read()
    row = [num(a, r'Victory\s+\d+\s+([\d.]+)%'), num(a, r'Deaths\s+\d+\s+([\d.]+)%'),
           num(a, r'Charges \(per fight\)\s+([\d.]+)'),
           num(b, r'Victory\s+\d+\s+([\d.]+)%'), num(b, r'Deaths\s+\d+\s+([\d.]+)%'),
           num(b, r'Escapes\s+\d+\s+([\d.]+)%')]
    print(f'| {n} | ' + ' | '.join(f'{v:.2f}' for v in row) + ' |')

print('\n### Campaign beds — 1600 dojos × 180 days, six seeds, mean (min-max)\n')
print('| Bed | Tier | Last night won | Played the night | Dojos closed | Deaths / warrior-fight | Net / fight | Bouts won |')
print('|---|---|---|---|---|---|---|---|')
for bed in ('plain', 'rich'):
    for tier in ('apprentice', 'master', 'legend'):
        files = sorted(glob.glob(f'{root}/campaign/{bed}-{tier}-s*.txt'))
        if not files:
            continue
        cols = {k: [] for k in ('night', 'played', 'closed', 'deaths', 'net', 'bouts')}
        for f in files:
            t = open(f, encoding='utf-8', errors='replace').read()
            cols['night'].append(num(t, r'won all five\s+([\d.]+)%'))
            cols['played'].append(num(t, r'Played it\s+([\d.]+)%'))
            cols['closed'].append(num(t, r'Dojos closed\s+([\d.]+)%'))
            cols['deaths'].append(num(t, r'Deaths / warrior-fight\s+([\d.]+)%'))
            cols['net'].append(num(t, r'Net\s+(-?[\d.]+)'))
            cols['bouts'].append(num(t, r'Bouts won\s+([\d.]+)'))
        cell = lambda k, pct: (f"{st.mean(cols[k]):.2f}{'%' if pct else ''} ({min(cols[k]):.1f}-{max(cols[k]):.1f})")
        print(f"| {bed} | {tier} | {cell('night', 1)} | {cell('played', 1)} | {cell('closed', 1)} | "
              f"{cell('deaths', 1)} | {cell('net', 0)} | {cell('bouts', 0)} |")
