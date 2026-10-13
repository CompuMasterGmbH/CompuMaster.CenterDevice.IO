"""Persist current public-workflow evidence, not an API-parity certification."""
import argparse, csv, hashlib, io, json, re, subprocess, tarfile
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source-ref', required=True, help='Exact immutable commit containing the audited source.')
parser.add_argument('--output', type=Path, required=True, help='CSV output path; never overwrites the historical baseline.')
args = parser.parse_args()
repo = Path(__file__).resolve().parents[2]
if not re.fullmatch(r'[0-9a-f]{40}', args.source_ref):
    raise SystemExit('An exact 40-character commit is required.')

def git(*arguments):
    return subprocess.check_output(['git', *arguments], cwd=repo)

resolved = git('rev-parse', args.source_ref + '^{commit}').decode().strip()
if resolved != args.source_ref:
    raise SystemExit('The source commit did not resolve exactly.')
source_tree = git('rev-parse', args.source_ref + ':CenterDevice.Rest').decode().strip()
archive = git('archive', args.source_ref, '--', 'CenterDevice.Rest')
with tarfile.open(fileobj=io.BytesIO(archive)) as content:
    files = {}
    for item in content.getmembers():
        if item.isfile() and item.name.endswith('.cs'):
            data = content.extractfile(item).read()
            try:
                text = data.decode('utf-8-sig')
            except UnicodeDecodeError:
                text = data.decode('cp1252')
            files[item.name] = (text, hashlib.sha256(data).hexdigest())
root = Path(__file__).resolve().parent
mapping = json.loads((root / 'current-map.json').read_text(encoding='utf-8-sig'))
with (root / 'coverage.csv').open(encoding='utf-8-sig', newline='') as baseline:
    rows = list(csv.DictReader(baseline))
sections = [row['Section'] for row in rows]
if len(sections) != 169 or len(set(sections)) != 169 or set(mapping) != set(sections):
    raise SystemExit('The current map must cover every one of the 169 distinct pinned sections exactly.')
for row in rows:
    row['Initial inventory live verification'] = row.pop('Live verification')
    item = mapping[row['Section']]
    evidence = item['entryPoint']
    matches = []
    if evidence:
        class_name, separator, method = evidence.partition('.')
        class_pattern = r'\bclass\s+' + re.escape(class_name) + r'\b'
        method_pattern = r'\bpublic\s+(?:async\s+)?[^\r\n{;=]+?\s+' + re.escape(method) + r'(?:<[^>]+>)?\s*\('
        matches = [(path, digest) for path, (text, digest) in files.items()
                   if re.search(class_pattern, text) and (not separator or re.search(method_pattern, text))]
        if not matches:
            raise SystemExit('Missing public source evidence: ' + row['Section'] + ' ' + evidence)
    row.update({'Current source commit': args.source_ref, 'Production subtree': source_tree,
                'Current public workflow/model': evidence or 'No dedicated legacy GET entry point',
                'Current source files/SHA256': ' ; '.join(path + ' ' + digest for path, digest in sorted(matches)),
                'Current source assessment': item['sourceStatus'],
                'Request-model parity': 'Complete per-operation comparison remains separate; extracted keys above are reference hints, not proof.',
                'Current bounded live evidence': item['liveEvidence'],
                'Remaining acceptance': 'Complete current role/feature/server matrix, approved publication and exact downstream adoption.'})
if args.output.resolve() == (root / 'coverage.csv').resolve():
    raise SystemExit('The initial baseline inventory is immutable.')
args.output.parent.mkdir(parents=True, exist_ok=True)
with args.output.open('w', encoding='utf-8-sig', newline='') as output:
    writer = csv.DictWriter(output, fieldnames=rows[0].keys(), lineterminator='\r\n')
    writer.writeheader(); writer.writerows(rows)
missing = [row['Section'] for row in rows if not mapping[row['Section']]['entryPoint']]
print(f'{len(rows)} pinned sections; {len(rows)-len(missing)} public workflow/model mappings; missing dedicated routes: {missing}.')
print('This evidence does not certify complete parameter/model/permission parity or current live acceptance.')
