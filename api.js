/* api.js — replaces db.js + auth.js in the browser.
   Same function names as before, but every data function is now async and
   talks to the C# backend (same origin, /api/...). Session = HttpOnly cookie. */

const ROLE_LABELS = { administrator: 'Administrator', staff: 'Staff' };

async function api(method, path, body){
  const res = await fetch('/api' + path, {
    method,
    credentials: 'same-origin',
    headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  let data = null;
  if(res.status !== 204){ try{ data = await res.json(); }catch(e){} }
  if(!res.ok){
    const msg = (data && data.error) ||
      (res.status === 401 ? 'Please sign in.' :
       res.status === 403 ? 'You do not have permission to do that.' :
       'Request failed (' + res.status + ').');
    const err = new Error(msg);
    err.status = res.status;
    throw err;
  }
  return data;
}
const enc = encodeURIComponent;

/* ---------- Session / auth ---------- */
let _session;
async function getSession(){
  if(_session !== undefined) return _session;
  try{ _session = await api('GET', '/auth/me'); }catch(e){ _session = null; }
  return _session;
}
async function requireAuth(allowedRoles, loginPage){
  const s = await getSession();
  if(!s){ window.location.href = loginPage || 'index.html'; return null; }
  if(allowedRoles && !allowedRoles.includes(s.role)){ window.location.href = 'dashboard.html'; return null; }
  return s;
}
async function attemptLogin(username, password, portal, remember){
  const user = await api('POST', '/auth/login', { username, password, portal, remember });
  _session = undefined;
  return user;
}
async function logout(){
  try{ await api('POST', '/auth/logout'); }catch(e){}
  _session = null;
  window.location.href = 'index.html';
}
function updateUserName(_id, name){ return api('PUT', '/auth/me/name', { name }); }
function changeMyPassword(current, next){ return api('POST', '/auth/me/password', { current, next }); }

/* ---------- Users (admin) ---------- */
function listUsers(){ return api('GET', '/users'); }
function addStaffAccount({ name, username, password, role }){ return api('POST', '/users', { name, username, password, role }); }
function resetUserPassword(id, newPassword){ return api('POST', '/users/' + enc(id) + '/reset-password', { newPassword }); }
function removeUser(id){ return api('DELETE', '/users/' + enc(id)); }

/* ---------- Products ---------- */
function listProducts(){ return api('GET', '/products'); }
async function getProductById(id){
  try{ return await api('GET', '/products/' + enc(id)); }
  catch(e){ if(e.status === 404) return null; throw e; }
}
function addProduct(data){ return api('POST', '/products', data); }
function updateProduct(id, data){ return api('PUT', '/products/' + enc(id), data); }
function deleteProduct(id){ return api('DELETE', '/products/' + enc(id)); }

/* ---------- Categories ---------- */
function listCategories(){ return api('GET', '/categories'); }
function addCategory(name){ return api('POST', '/categories', { name }); }
function updateCategory(name, nextName){ return api('PUT', '/categories/' + enc(name), { name: nextName }); }
async function deleteCategory(name){ return (await api('DELETE', '/categories/' + enc(name))).movedProducts; }

/* ---------- Transactions ---------- */
function addTransaction({ productId, type, qty, note }){
  return api('POST', '/transactions', { productId, type, qty: Number(qty), note });
}
async function listTransactions(type){
  const q = type && type !== 'all' ? '?type=' + enc(type) : '';
  const rows = await api('GET', '/transactions' + q);
  return rows.map(t => ({ ...t, at: new Date(t.at).getTime() }));   // ms timestamp, like before
}

/* ---------- KPIs / alerts ---------- */
function computeKPIs(){ return api('GET', '/kpis'); }
function getLowStockProducts(limit){ return api('GET', '/products/low-stock' + (limit ? '?limit=' + limit : '')); }

/* ---------- Activity log / demo data (admin) ---------- */
async function listActivity(){
  const rows = await api('GET', '/activity');
  return rows.map(l => ({ ...l, at: new Date(l.at).getTime() }));
}
function clearActivity(){ return api('DELETE', '/activity'); }
function resetDB(){ return api('POST', '/admin/reset-demo-data'); }

/* ---------- Formatting helpers (unchanged) ---------- */
function formatTimeAgo(ts){
  const diff = Date.now() - ts;
  const day = 24*60*60*1000;
  if(diff < day) return new Date(ts).toLocaleTimeString([], {hour:'2-digit', minute:'2-digit'});
  if(diff < 2*day) return 'Yesterday';
  return new Date(ts).toLocaleDateString();
}
function money(n){
  return '$' + Number(n).toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}
function initials(name){
  return (name || '?').split(' ').map(w=>w[0]).slice(0,2).join('').toUpperCase();
}
// Data now lives in a shared database, so escape text before putting it in innerHTML
function esc(v){
  return String(v ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
}
