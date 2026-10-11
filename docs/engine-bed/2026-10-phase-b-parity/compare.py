# Phase B's parity bed: reads a run of run-phase-b-parity.sh against the pre-engine baseline.
# Usage: python compare.py <baseline-folder> <run-folder>
#
#   1. calibration  core/ against the baseline, line for line but the wall clock
#   2. determinism  engine-again/ against engine/, line for line but the wall clock
#   3. duel parity  every count-and-share line of every duel report, engine/ against core/, as a z-score
#                   on two independent samples of the report's own size
#   4. campaign     every closing figure of the campaign beds, engine/ against core/, as a z-score on the
#                   six seeds' own spread
#   5. throughput   the bed's wall clock and motion tests a fight, from */bed.txt
import glob, math, os, re, statistics as st, sys

base, run = sys.argv[1], sys.argv[2]


def normalised(path):
    """The report's lines without the wall clock; the baseline was written in the console's code page,
    the bed writes UTF-8, so the dash and the times sign are read as the code page wrote them and the
    rest of the non-ASCII marks are dropped."""
    text = open(path, 'rb').read().decode('utf-8', errors='replace')
    text = text.replace(chr(0x2014), '-').replace(chr(0xd7), 'x')
    text = ''.join(c for c in text if ord(c) < 128)
    return [l.rstrip() for l in text.splitlines() if not l.startswith('Wall clock')]


def reports(folder):
    return sorted(os.path.relpath(p, folder).replace('\\', '/')
                  for p in glob.glob(f'{folder}/duel/*.txt') + glob.glob(f'{folder}/campaign/*.txt'))


def identical(a, b, names):
    same, differ, missing = 0, [], []
    for n in names:
        if not os.path.exists(f'{b}/{n}') or os.path.getsize(f'{b}/{n}') == 0:
            missing.append(n)
        elif normalised(f'{a}/{n}') == normalised(f'{b}/{n}'):
            same += 1
        else:
            differ.append(n)
    return same, differ, missing


print('## 1. Calibration: the resolver\'s own kinematics inside the bed against the baseline\n')
names = reports(base)
same, differ, missing = identical(base, f'{run}/core', names)
print(f'identical {same} of {len(names)}; differ: {differ or "none"}; missing: {missing or "none"}\n')

print('## 2. Determinism: the engine\'s ground run twice\n')
again = reports(f'{run}/engine-again')
same, differ, missing = identical(f'{run}/engine', f'{run}/engine-again', again)
print(f'identical {same} of {len(again)}; differ: {differ or "none"}\n')


def shares(path):
    """(section, label) -> (count, size) for every 'label  count  share%' line."""
    out, fights, size, section = {}, None, None, ''
    for line in normalised(path):
        if m := re.match(r'Fights\s*:\s*(\d+)', line):
            fights = int(m.group(1))
        elif line.startswith('Outcome'):
            section, size = 'outcome', fights
        elif m := re.match(r'(Player|Enemy) side \((\d+) warrior-fights\)', line):
            section, size = m.group(1).lower(), int(m.group(2))
        elif (m := re.match(r'\s+(\S.*?)\s{2,}(\d+)\s+[\d.]+%$', line)) and size:
            out[(section, m.group(1))] = (int(m.group(2)), size)
    return out


def means(path):
    t = '\n'.join(normalised(path))
    grab = lambda p: float(m.group(1)) if (m := re.search(p, t)) else float('nan')
    return {'fight s': grab(r'Fight duration\s*:\s*([\d.]+)'), 'hit rate': grab(r'Hit rate\s+([\d.]+)%'),
            'charges': grab(r'Charges \(per fight\)\s+([\d.]+)'), 'blocks': grab(r'Blocks \(per warrior\)\s+([\d.]+)')}


def z(c1, n1, c2, n2):
    p1, p2 = c1 / n1, c2 / n2
    se = math.sqrt(p1 * (1 - p1) / n1 + p2 * (1 - p2) / n2)
    return 0.0 if se == 0 else (p2 - p1) / se


print('## 3. Duel parity: the engine\'s ground against the resolver\'s own kinematics\n')
print('Each share line of each report (outcome per fight, deaths/escapes/limbs per warrior-fight). '
      'z is the difference over the standard error of two independent samples of the report\'s size; '
      'warrior-fights of one fight are not independent, so for the side lines this errs strict.\n')
zs, worst = [], []
print('| Report | Victory | z | Deaths | z | Escapes | z | Fight s | Hit rate | Charges | Max abs z (line) |')
print('|---|---|---|---|---|---|---|---|---|---|---|')
for n in [n for n in names if n.startswith('duel/')]:
    c, e = f'{run}/core/{n}', f'{run}/engine/{n}'
    if not os.path.exists(e):
        continue
    sc, se_ = shares(c), shares(e)
    row_z = {}
    for k, (cc, nc) in sc.items():
        if k in se_:
            ce, ne = se_[k]
            row_z[k] = z(cc, nc, ce, ne)
            zs.append(row_z[k])
            worst.append((abs(row_z[k]), n, k, cc / nc * 100, ce / ne * 100))
    pct = lambda s, k: s[k][0] / s[k][1] * 100 if k in s else float('nan')
    cell = lambda k: (f'{pct(sc, k):.2f} > {pct(se_, k):.2f}', f'{row_z.get(k, float("nan")):+.1f}')
    mc, me = means(c), means(e)
    top = max(row_z.items(), key=lambda kv: abs(kv[1]))
    v, d, s = cell(('outcome', 'Victory')), cell(('player', 'Deaths')), cell(('player', 'Escapes'))
    print(f'| {n[5:-4]} | {v[0]} | {v[1]} | {d[0]} | {d[1]} | {s[0]} | {s[1]} | '
          f'{mc["fight s"]:.1f} > {me["fight s"]:.1f} | {mc["hit rate"]:.1f} > {me["hit rate"]:.1f} | '
          f'{mc["charges"]:.2f} > {me["charges"]:.2f} | {abs(top[1]):.1f} ({top[0][0]} {top[0][1]}) |')

if zs:
    count = len(zs)
    over2 = sum(abs(x) > 2 for x in zs)
    over3 = sum(abs(x) > 3 for x in zs)
    print(f'\n{count} share lines: |z| > 2 on {over2} ({100 * over2 / count:.1f}%, chance alone ~4.6%), '
          f'|z| > 3 on {over3} (chance alone ~{0.0027 * count:.1f}); mean z {st.mean(zs):+.2f}, '
          f'sd {st.pstdev(zs):.2f}\n')
    print('Largest ten:\n')
    for a, n, k, pc, pe in sorted(worst, reverse=True)[:10]:
        print(f'- {n[5:-4]}: {k[0]} {k[1]} {pc:.2f}% > {pe:.2f}% (z {a:.1f})')
    print()

print('## 4. Campaign parity: paired means over six seeds (the protocol locked 2026-09-13)\n')
print('Each cell: the recorded value (core) > the engine\'s, then the paired difference against the recorded '
      'value\'s own noise, two standard errors of its six-seed mean. Inside means the difference is smaller '
      'than that band; t is the paired difference over its own standard error (5 degrees of freedom: '
      '|t| > 2.57 is 5%, > 4.03 is 1%).\n')
pats = {'last night won': r'won all five\s+([\d.]+)%', 'played the night': r'Played it\s+([\d.]+)%',
        'dojos closed': r'Dojos closed\s+([\d.]+)%', 'deaths / warrior-fight': r'Deaths / warrior-fight\s+([\d.]+)%',
        'net / fight': r'Net\s+(-?[\d.]+)', 'bouts won': r'Bouts won\s+([\d.]+)'}
print('| Bed | Tier | ' + ' | '.join(pats) + ' |')
print('|---|---|' + '---|' * len(pats))
inside = outside = 0
for bed in ('plain', 'rich'):
    for tier in ('apprentice', 'master', 'legend'):
        files = [os.path.basename(f) for f in sorted(glob.glob(f'{run}/core/campaign/{bed}-{tier}-s*.txt'))]
        files = [f for f in files if os.path.exists(f'{run}/engine/campaign/{f}')]
        if len(files) < 2:
            continue
        cells = []
        for label, p in pats.items():
            vals = {}
            for side in ('core', 'engine'):
                vals[side] = [float(re.search(p, '\n'.join(normalised(f'{run}/{side}/campaign/{f}'))).group(1))
                              for f in files]
            diffs = [e - c for c, e in zip(vals['core'], vals['engine'])]
            band = 2 * st.stdev(vals['core']) / math.sqrt(len(files))
            sd = st.stdev(diffs)
            t = 0.0 if sd == 0 else st.mean(diffs) / (sd / math.sqrt(len(files)))
            ok = abs(st.mean(diffs)) <= band
            inside, outside = inside + ok, outside + (not ok)
            cells.append(f'{st.mean(vals["core"]):.2f} > {st.mean(vals["engine"]):.2f}: '
                         f'{st.mean(diffs):+.2f} of +/-{band:.2f} {"inside" if ok else "**OUTSIDE**"} (t {t:+.1f})')
        print(f'| {bed} | {tier} | ' + ' | '.join(cells) + ' |')
print(f'\n{inside + outside} campaign figures: {inside} inside the recorded value\'s noise, {outside} outside\n')

print('## 5. Throughput (the bed\'s wall clock per report, summed over runs made side by side)\n')
for side in ('core', 'engine'):
    path = f'{run}/{side}/bed.txt'
    if not os.path.exists(path):
        continue
    secs = {'duel': 0.0, 'campaign': 0.0}
    tests = fights = 0
    for line in open(path, encoding='utf-8', errors='replace'):
        kind = 'duel' if line.startswith('duel') else 'campaign'
        if m := re.search(r'seconds=([\d.]+)', line):
            secs[kind] += float(m.group(1))
        if m := re.search(r'fights (\d+); motion tests (\d+)', line):
            fights += int(m.group(1))
            tests += int(m.group(2))
    extra = f'; {fights} fights, {tests / fights:.1f} motion tests a fight' if fights else ''
    print(f'- {side}: duel bed {secs["duel"]:.0f} s, campaign beds {secs["campaign"]:.0f} s{extra}')
