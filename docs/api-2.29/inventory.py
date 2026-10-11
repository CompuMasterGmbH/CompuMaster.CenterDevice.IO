import argparse, csv, hashlib, re, subprocess
from pathlib import Path

parser = argparse.ArgumentParser(description='Build an initial API 2.29 section inventory; does not certify contract parity.')
parser.add_argument('--repo', type=Path, default=Path(__file__).resolve().parents[2])
parser.add_argument('--reference-text', required=True, help='Page-marked UTF-8 text extraction beside the pinned .pdf; never commit credentials or the research directory.')
args = parser.parse_args()
repo = Path(args.repo)
source = Path(args.reference_text)
pdf = source.with_suffix('.pdf')
assert hashlib.sha256(pdf.read_bytes()).hexdigest() == '5edf3d5d916e82b60527a9e5c8e5bd1f01a836d3c6a526dcf631414f94fc00d6'
text = source.read_text(encoding='utf-8-sig')
toc, body = text.split('=== PDF PAGE 11 ===', 1)
entries = re.findall(r'^(5\.\d+\.\d+) (.*?)\s+\. .+? (\d+)\s*$', toc, re.M)
assert len(entries) > 140, len(entries)
heads = list(re.finditer(r'^(5\.\d+\.\d+) (.+)$', body, re.M))
sections = {}
for i, h in enumerate(heads):
    if h.group(1) not in sections:
        sections[h.group(1)] = body[h.end():heads[i+1].start() if i+1 < len(heads) else len(body)]
mapping = {}
def add(group, pairs):
    for number, evidence in pairs.items():
        mapping[f'5.{group}.{number}'] = evidence
add(2, {1:'DocumentsRestClient.UploadDocument: limited upload actions', 2:'DocumentRestClient.UploadNewVersion', 4:'DocumentRestClient.RenameDocument', 6:'DocumentRestClient.DeleteDocument', 7:'DocumentsRestClient.DeleteDocuments', 10:'DocumentRestClient.DownloadDocument', 11:'DocumentRestClient.DownloadDocument: start-offset range only', 12:'DocumentRestClient.DownloadDocument: version parameter', 16:'DocumentRestClient.DownloadPreview: PreviewSize only', 20:'DocumentRestClient.GetDocumentMetadata<T>', 21:'DocumentsRestClient.Get<T>: limited filters', 22:'DocumentsRestClient.Search<T>: limited DocumentSearchRequest', 31:'DocumentRestClient.AddLock: create-new-version only', 33:'DocumentRestClient.RemoveLock: create-new-version only', 37:'DocumentsRestClient.MoveDocuments'})
add(3, {1:'CollectionsRestClient.CreateCollection: name only', 2:'CollectionRestClient.RenameCollection: name only', 3:'CollectionRestClient.AddDocumentToCollection: one ID', 4:'CollectionRestClient.RemoveDocumentFromCollection: one ID', 5:'CollectionRestClient.ShareCollection', 6:'CollectionRestClient.UnshareCollection', 14:'CollectionRestClient.ArchiveCollection: basic command', 18:'CollectionRestClient.EraseCollection', 19:'CollectionRestClient.DeleteCollection', 20:'CollectionRestClient.GetCollection', 21:'CollectionsRestClient.GetCollections'})
add(4, {1:'FoldersRestClient.CreateFolder', 2:'FolderRestClient.RenameFolder', 3:'FolderRestClient.MoveFolder', 4:'FolderRestClient.AddDocument: one ID', 5:'FolderRestClient.RemoveDocument: one ID', 6:'FolderRestClient.ShareFolder', 7:'FolderRestClient.UnshareFolder', 8:'FolderRestClient.EraseFolder', 9:'FolderRestClient.DeleteFolder', 10:'FolderRestClient.GetFolder', 11:'FoldersRestClient.GetFolders'})
add(6, {1:'LinksRestClient.CreateDocumentLink/CreateFolderLink/CreateCollectionLink', 2:'LinkRestClient.GetLink', 3:'LinkRestClient.UpdateLink', 4:'LinkRestClient.DeleteLink'})
add(7, {1:'UploadLinksRestClient.CreateCollectionLink', 2:'UploadLinkRestClient.GetLink', 3:'UploadLinksRestClient.GetAllUploadLinks', 4:'UploadLinkRestClient.UpdateLink', 5:'UploadLinkRestClient.DeleteLink'})
add(8, {2:'UserRestClient.GetUserData/GetUserDataField/GetLoggedInUserData', 3:'UsersRestClient.GetAllUsers'})
add(9, {2:'UserSettingsRestClient.GetUserSettings'})
add(11, {9:'GroupRestClient.GetGroup', 10:'GroupsRestClient.GetAllGroups'})
add(12, {2:'TenantRestClient.GetTenant', 3:'TenantsRestClient.GetTenants'})
add(13, {1:'TenantSettingsRestClient.UpdateTenantSettings: one setting', 2:'TenantSettingsRestClient.GetTenantSettings'})
add(14, {1:'TenantFeaturesRestClient.GetTenantFeatures', 2:'TenantFeaturesRestClient.EnableTenantFeature'})
add(15, {1:'TimelineRestClient.GetTimelineEvents: limited filter', 2:'TimelineRestClient.Scroll: limited filter', 3:'TimelineEntry: model drift pending'})
def read_code(data):
    try:
        return data.decode('utf-8-sig')
    except UnicodeDecodeError:
        return data.decode('cp1252')
baseline = '0b89c9ae06735b228e01c4816842772e2b475cb0'
paths = subprocess.check_output(['git','ls-tree','-r','--name-only',baseline,'--','CenterDevice.Rest/'],cwd=repo).decode().splitlines()
clients = '\n'.join(read_code(subprocess.check_output(['git','show',baseline+':'+p],cwd=repo)) for p in paths if p.endswith('.cs'))
rows = []
for section, title, page in entries:
    if section == '5.1.1':
        continue
    assert section in sections, section
    chunk = sections[section]
    request = chunk.split('Request', 1)[1].split('\nResponse', 1)[0] if 'Request' in chunk else chunk.split('\nResponse',1)[0]
    routes = re.findall(r'\b(GET|POST|PUT|DELETE|PATCH)\s*(<BASE-URL>[^\n]*)', request)
    action = re.findall(r'[“\"]action[”\"]\s*:\s*[“\"]([^”\"]+)', request)
    keys = sorted(set(re.findall(r'[“\"]([a-z][a-z0-9_-]*)[”\"]\s*:', request)))
    restriction = chunk.split('Access and visibility restrictions',1)[1].split('\nRequest',1)[0] if 'Access and visibility restrictions' in chunk else ''
    low = restriction.lower()
    roles = [role for role, needle in [('admin','admin'),('internal','internal'),('external','external'),('guest','guest')] if needle in low]
    access = ', '.join(roles) if roles else 'General role/access contract; inspect cited section'
    ownership = '; '.join(label for label, needle in [('ownership restriction','own'),('visibility restriction','visib'),('lock restriction','lock'),('sharing restriction','shar'),('tenant restriction','tenant')] if needle in low) or 'General visible-resource contract; inspect cited section'
    availability = '; '.join(label for label, needle in [('feature-dependent','feature'),('archive-dependent','archiv'),('tenant-dependent','tenant')] if needle in chunk.lower()) or 'Not explicitly flagged by extraction; verify section'
    paging = ', '.join(key for key in ['offset','rows','scroll','previous-id','limit'] if key in request.lower()) or 'No paging parameter identified in request extraction'
    evidence = mapping.get(section, '')
    if evidence:
        for method in re.findall(r'(?:RestClient\.|/)([A-Z][A-Za-z]+)', evidence):
            assert method in clients, (section, method)
    status = 'Present entry point; parameter/model parity unverified' if evidence else 'No dedicated entry point/model found'
    if 'entry types' in title.lower():
        status = 'Event model inventory; not an HTTP operation'
    rows.append({'Section':section, 'Page':page, 'Operation':title, 'Method/route':' ; '.join(' '.join(r) for r in routes), 'Command action':','.join(action), 'Request keys':','.join(keys), 'Role mentions (not grants)':access, 'Ownership/sharing restriction keywords':ownership, 'Paging':paging, 'Availability keywords':availability, 'Restriction review':'Manual permission/feature confirmation pending; inspect cited section', 'Baseline coverage':status, 'Baseline evidence':evidence or 'Protected generic Execute/ExecuteAsync does not supply this public workflow', 'Live verification':'Not performed in this development cycle'})
out = repo/'docs'/'api-2.29'
assert len(rows) == 169, len(rows)
assert len({r['Section'] for r in rows}) == len(rows)
assert all(r['Method/route'] for r in rows if 'entry types' not in r['Operation'].lower())
out.mkdir(parents=True, exist_ok=True)
with (out/'coverage.csv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=rows[0].keys(),lineterminator='\r\n')
    writer.writeheader(); writer.writerows(rows)
print(f'{len(rows)} reference sections; {sum(bool(r["Baseline evidence"].startswith("Protected")) for r in rows)} without dedicated baseline entry points; extraction is an inventory, not a parity certification.')
print('Missing routes:', [(r['Section'],r['Operation']) for r in rows if not r['Method/route'] and 'entry types' not in r['Operation'].lower()])
