#!/usr/bin/env python3
"""End-to-end regression checks against an isolated temporary SQLite database.
Run `dotnet build` then `python3 tests/smoke.py`. No third-party Python packages.
"""
import datetime as dt
import html
import json
import re
from html.parser import HTMLParser
import http.cookiejar
import os
from pathlib import Path
import socket
import sqlite3
import subprocess
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
class FormParser(HTMLParser):
    def __init__(self):
        super().__init__(); self.values = {}; self.select = None; self.textarea = None
    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag == 'input' and a.get('name'): self.values[a['name']] = a.get('value', '')
        elif tag == 'select': self.select = a.get('name')
        elif tag == 'option' and self.select and 'selected' in a: self.values[self.select] = a.get('value', '')
        elif tag == 'textarea': self.textarea = a.get('name'); self.values[self.textarea] = ''
    def handle_endtag(self, tag):
        if tag == 'select': self.select = None
        elif tag == 'textarea': self.textarea = None
    def handle_data(self, value):
        if self.textarea: self.values[self.textarea] += value

def main():
    checks = 0
    def check(condition, message):
        nonlocal checks
        if not condition: raise AssertionError(message)
        checks += 1
        print(f'PASS {checks:02d} {message}')
    with tempfile.TemporaryDirectory(prefix='property-tests-') as tmp:
        with socket.socket() as sock:
            sock.bind(('127.0.0.1', 0)); port = sock.getsockname()[1]
        base = f'http://127.0.0.1:{port}'
        database = Path(tmp) / 'test.db'
        env = dict(os.environ, ASPNETCORE_ENVIRONMENT='Development', ConnectionStrings__PropertyDb=f'Data Source={database}')
        log = open(Path(tmp) / 'server.log', 'w+')
        server = subprocess.Popen(['dotnet', str(ROOT / 'bin/Debug/net10.0/PropertyManagement.dll'), '--urls', base], cwd=ROOT, env=env, stdout=log, stderr=log)
        opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        def request(path, data=None):
            req = urllib.request.Request(base+path, data=None if data is None else urllib.parse.urlencode(data).encode())
            try:
                with opener.open(req, timeout=15) as response: return response.status, html.unescape(response.read().decode())
            except urllib.error.HTTPError as ex: return ex.code, html.unescape(ex.read().decode())
        def form(module, id=0):
            status, body = request(f'/manage/{module}/'+(f'edit/{id}' if id else 'create'))
            check(status == 200 and '@f.Label' not in body and '請選擇狀態' in body, f'{module} form available')
            p = FormParser(); p.feed(body); return p.values
        def save(module, values, id=0): return request(f'/manage/{module}/save/{id}', values)
        def row(table, code):
            with sqlite3.connect(database) as con:
                con.row_factory = sqlite3.Row
                return con.execute(f'SELECT * FROM "{table}" WHERE Code=?', (code,)).fetchone()
        def count(table):
            with sqlite3.connect(database) as con: return con.execute(f'SELECT COUNT(*) FROM "{table}"').fetchone()[0]
        try:
            for _ in range(100):
                if server.poll() is not None: raise RuntimeError('Server exited')
                try:
                    if request('/')[0] == 200: break
                except (OSError, urllib.error.URLError): pass
                time.sleep(.1)
            else: raise RuntimeError('Startup timed out')
            status, dashboard = request('/')
            check(status == 200 and 'id="property-scene"' in dashboard and 'id="property-canvas"' in dashboard,
                  'dashboard offers an interactive property scene')
            scene_match = re.search(r'<script type="application/json" id="property-scene-data">(.*?)</script>', dashboard, re.S)
            check(scene_match is not None, 'property scene data is available to the browser')
            scene_data = json.loads(scene_match.group(1))
            check(len(scene_data) == count('Assets') and sum(len(a['units']) for a in scene_data) == count('Units'),
                  'property scene includes actual assets and units')
            check('data-select-asset=' in dashboard and 'data-select-unit=' in dashboard and '/manage/units/edit/' in dashboard,
                  'property navigation remains available without WebGL')
            check(request('/js/property-scene.js')[0] == 200 and request('/vendor/three/three.module.js')[0] == 200,
                  '3D application and engine are served locally')
            check('<script type="importmap">' in dashboard,
                  'browser receives the import map required by the 3D engine')
            injury_status, injury_page = request('/injuries')
            check(injury_status == 200 and 'id="injury-canvas"' in injury_page,
                  'injury management provides a 3D body map')
            check('href="/injuries" class="nav-item active"' in injury_page,
                  'injury management is reachable from the main navigation')
            injury_json = re.search(r'<script type="application/json" id="injury-body-data">(.*?)</script>', injury_page, re.S)
            injury_parts = json.loads(injury_json.group(1))
            check(len(injury_parts) >= 80 and len({p['id'] for p in injury_parts}) == len(injury_parts),
                  'body map exposes distinct detailed injury locations')
            check(injury_page.count('name="bodyParts"') == len(injury_parts) and 'left-finger-1' in injury_page and 'right-toe-5' in injury_page,
                  'every injury location has an accessible checkbox alternative')
            check(request('/js/injury-body.js')[0] == 200 and request('/css/injury.css')[0] == 200,
                  'injury interaction and presentation assets are available')
            for module in ['assets','units','tenants','leases','meters','readings','invoices','payments','einvoices']:
                check(request('/manage/'+module)[0] == 200, f'{module} list renders')
                form(module)
            status, body = request('/manage/units?page=2&size=10')
            check(status == 200 and '顯示 11–20 筆' in body and 'UNT-011' in body and 'UNT-001' not in body, 'server-side page 2 contains only the correct records')
            check('顯示 1–20 筆' in request('/manage/units?size=20')[1], 'page size 20 applied')
            check('顯示 31–32 筆' in request('/manage/units?page=999')[1], 'out-of-range page clamped')
            check('顯示 1–10 筆' in request('/manage/units?page=-1&size=-5')[1], 'invalid page size safely normalized')
            check('UNT-032' in request('/manage/units?q=UNT-032')[1] and 'UNT-001' not in request('/manage/units?q=UNT-032')[1], 'keyword search filters records')
            check('顯示 1–1 筆' in request('/manage/units?status='+urllib.parse.quote('維修中'))[1], 'status filter returns matching records')
            check(request('/manage/unknown')[0] == 404, 'unknown module rejected')
            check(request('/manage/assets/edit/2147483000')[0] == 404, 'unknown record rejected')
            check(request('/manage/assets/save/0', {'Code':'CSRF'})[0] == 400, 'missing antiforgery token rejected')
            scene_name = '測試資產</script><script>alert(1)</script>'
            data = form('assets'); data.update(Code='QA-ASSET',Name=scene_name,Address='測試路 1 號',Company='測試公司',Manager='測試員',Area='25.75')
            check(save('assets',data)[0] == 200 and row('Assets','QA-ASSET') is not None, 'create asset persisted')
            asset = row('Assets','QA-ASSET'); aid=asset['Id']
            scene_payload = re.search(r'<script type="application/json" id="property-scene-data">(.*?)</script>', request('/')[1], re.S).group(1)
            check('</script' not in scene_payload.lower() and next(a for a in json.loads(scene_payload) if a['id'] == aid)['name'] == scene_name,
                  'scene JSON safely preserves names containing script markup')
            check(asset['Area'] == 2575, 'decimal values stored without floating-point loss')
            data = form('assets'); data.update(Code='QA-ASSET',Name='重複',Address='測試',Company='測試',Manager='測試',Area='1')
            check(save('assets',data)[0] == 422, 'duplicate code blocked')
            data = form('assets',aid); stale=dict(data); data['Name']='修改後資產'
            check(save('assets',data,aid)[0] == 200 and row('Assets','QA-ASSET')['Name']=='修改後資產', 'edit asset persisted')
            stale['Name']='過期修改'; check(save('assets',stale,aid)[0] == 422 and row('Assets','QA-ASSET')['Name']=='修改後資產','stale update cannot overwrite newer changes')
            data = form('units'); data.update(Code='QA-UNIT',Name='測試單位',AssetId=str(aid),Floor='2F',Area='10')
            check(save('units',data)[0] == 200,'create related unit'); uid=row('Units','QA-UNIT')['Id']
            data = form('tenants');data.update(Code='QA-TENANT',Name='測試承租戶',Contact='測試人',Phone='0912345678')
            check(save('tenants',data)[0] == 200,'create tenant');tid=row('Tenants','QA-TENANT')['Id']
            tenants_list_body = request('/manage/tenants?q=QA-TENANT')[1]
            check('測○人' in tenants_list_body and '測試人' not in tenants_list_body, 'tenant contact is masked in list view')
            check(form('tenants', tid).get('Contact') == '測試人', 'tenant contact remains unmasked in edit form')
            data = form('leases');data.update(Code='QA-LEASE',Name='測試租約',UnitId=str(uid),TenantId=str(tid),StartDate='2026-01-16',EndDate='2027-01-15',Rent='31000',Deposit='62000',DueDay='5',Status='有效')
            check(save('leases',data)[0] == 200,'create active lease');lid=row('Leases','QA-LEASE')['Id']
            overlap=dict(data);overlap['Code']='QA-OVERLAP';overlap['Name']='重疊租約'
            check(save('leases',overlap)[0] == 422,'overlapping active lease blocked')
            before=count('Invoices'); token=form('invoices')['__RequestVerificationToken']
            check(request('/manage/invoices/generate',{'month':'2026-01','__RequestVerificationToken':token})[0]==200,'monthly rent generation succeeds')
            with sqlite3.connect(database) as con:
                amount=con.execute('SELECT Amount FROM Invoices WHERE LeaseId=? AND Period LIKE ?', (lid,'2026-01%')).fetchone()[0]
            check(amount==1600000,'partial month includes both dates and rounds to whole dollars')
            total=count('Invoices');request('/manage/invoices/generate',{'month':'2026-01','__RequestVerificationToken':token})
            check(count('Invoices')==total and total>before,'repeat generation does not duplicate invoices')
            leaseform=form('leases',lid);leaseform['Rent']='32000';check(save('leases',leaseform,lid)[0]==422,'issued lease financial terms protected')
            with sqlite3.connect(database) as con: iid=con.execute('SELECT Id FROM Invoices WHERE LeaseId=?',(lid,)).fetchone()[0]
            pay=form('payments');pay.update(Code='QA-PAY',Name='分次收款',InvoiceId=str(iid),PaidDate='2026-01-20',Amount='20000',Method='銀行轉帳')
            check(save('payments',pay)[0]==422,'overpayment blocked')
            pay['Amount']='6000';check(save('payments',pay)[0]==200,'partial payment accepted');pid=row('Payments','QA-PAY')['Id']
            pay2=form('payments');pay2.update(Code='QA-PAY2',Name='剩餘收款',InvoiceId=str(iid),PaidDate='2026-01-21',Amount='10000',Method='現金')
            check(save('payments',pay2)[0]==200,'remaining balance can be fully paid')
            check('class="numeric">0</td>' in request('/manage/invoices?q=RENT-202601-'+str(lid))[1],'fully paid invoice has zero remaining balance')
            inv=form('invoices',iid);inv['Amount']='15000';check(save('invoices',inv,iid)[0]==422,'invoice with receipt cannot be rewritten')
            pform=form('payments',pid);pform['Status']='作廢';check(save('payments',pform,pid)[0]==200,'receipt can be voided while retaining history')
            einv=form('einvoices');einv.update(Code='QA-EINV',Name='1月租金電子發票',InvoiceId=str(iid),TrackNumber='ab12345678',IssuedDate='2026-01-20',Category='B2B 存證',TaxType='應稅',BuyerTaxId='24536806',CarrierType='無載具',CarrierId='',DonateCode='',RandomCode='8888',Amount='16000')
            check(save('einvoices',einv)[0]==200,'create Taiwan B2B electronic invoice')
            saved_einv=row('EInvoices','QA-EINV');eid=saved_einv['Id']
            check(saved_einv['TrackNumber']=='AB-12345678' and saved_einv['SalesAmount']+saved_einv['TaxAmount']==1600000,'Taiwan track number normalized and 5% VAT split calculated')
            bad_einv=dict(einv);bad_einv['Code']='QA-EINV-DUP'
            check(save('einvoices',bad_einv)[0]==422,'duplicate Taiwan track number blocked')
            eform=form('einvoices',eid);eform['Amount']='10000';check(save('einvoices',eform,eid)[0]==422,'issued electronic invoice core fields locked')
            eform=form('einvoices',eid);eform['Status']='作廢';check(save('einvoices',eform,eid)[0]==200,'electronic invoice can be voided')
            etoken=form('einvoices')['__RequestVerificationToken']
            check(request('/manage/einvoices/generate',{'month':'2026-01','__RequestVerificationToken':etoken})[0]==200,'monthly electronic invoice batch generation succeeds')
            meter=form('meters');meter.update(Code='QA-METER',Name='測試電表',UnitId=str(uid),Kind='電表',Account='123456',Rate='5.5')
            check(save('meters',meter)[0]==200,'create meter');mid=row('Meters','QA-METER')['Id']
            reading=form('readings');reading.update(Code='QA-READING',Name='測試抄表',MeterId=str(mid),ReadDate='2026-01-31',Previous='100',Current='110.5',Rate='5.5')
            check(save('readings',reading)[0]==200 and row('Readings','QA-READING')['Amount']==5800,'meter calculation rounds 57.75 to 58 dollars')
            reading['Code']='QA-BAD-READING';reading['Current']='99';check(save('readings',reading)[0]==422,'negative meter usage blocked')
            invalid=form('assets',aid); invalid['Status']='停用';check(save('assets',invalid,aid)[0]==422,'asset with an active lease cannot be deactivated')
            invalid=form('units',uid); invalid['Status']='維修中';check(save('units',invalid,uid)[0]==422,'unit with an active lease cannot change to maintenance')
            invalid=form('meters',mid); invalid['UnitId']='7';check(save('meters',invalid,mid)[0]==422,'meter with readings cannot change unit')
            invalid=form('payments');invalid.update(Code='QA-DECIMAL',Name='小數收款',InvoiceId=str(iid),PaidDate='2026-01-20',Amount='0.5');check(save('payments',invalid)[0]==422,'financial amounts require whole dollars')
            invalid['Amount']='1';invalid['PaidDate']='2100-01-01';check(save('payments',invalid)[0]==422,'future receipt dates rejected')
            invalid['PaidDate']='2026-01-20';invalid['InvoiceId']='2147483000';check(save('payments',invalid)[0]==422,'unknown foreign key returns validation rather than exception')
            invalid=form('readings');invalid.update(Code='QA-LARGE',Name='超額抄表',MeterId=str(mid),ReadDate='2026-02-01',Previous='0',Current='999999999',Rate='999999999');check(save('readings',invalid)[0]==422,'calculated meter amount cannot overflow storage')
            check(request('/?month=9999-12')[0]==200,'out-of-range dashboard month handled safely')
            check(request('/manage/invoices/generate',{'month':'9999-12','__RequestVerificationToken':token})[0]==200,'out-of-range billing month rejected safely')
            protected_before=count('Payments');status,body=request('/manage/payments/delete/'+str(pid));parser=FormParser();parser.feed(body);request('/manage/payments/delete/'+str(pid),parser.values)
            check(count('Payments')==protected_before,'financial receipt deletion preserves history')
            # A separate unrelated record proves destructive action works without touching fixtures.
            unused=form('assets');unused.update(Code='QA-DELETE',Name='可刪除資產',Address='測試',Company='測試',Manager='測試',Area='1')
            save('assets',unused);deleteid=row('Assets','QA-DELETE')['Id'];status,body=request('/manage/assets/delete/'+str(deleteid));parser=FormParser();parser.feed(body)
            check(request('/manage/assets/delete/'+str(deleteid),parser.values)[0]==200 and row('Assets','QA-DELETE') is None,'unreferenced record can be deleted')
            status,body=request('/manage/assets/delete/'+str(aid));parser=FormParser();parser.feed(body)
            check(request('/manage/assets/delete/'+str(aid),parser.values)[0]==200 and row('Assets','QA-ASSET') is not None,'referenced record deletion blocked')
            print(f'\nAll {checks} checks passed. Database isolated and removed on completion.')
        except Exception:
            log.flush();log.seek(0);print(log.read()[-8000:]);raise
        finally:
            server.terminate()
            try:server.wait(timeout=8)
            except subprocess.TimeoutExpired:server.kill();server.wait()
            log.close()

if __name__=='__main__':main()
