/* layout.js — renders the sidebar + topbar shell used by every dashboard page. */

const NAV_ITEMS = [
  { key:'dashboard', label:'Dashboard',         href:'dashboard.html',         icon:'dashboard', roles:['administrator','staff'] },
  { key:'products',  label:'Products',          href:'products.html',          icon:'products',  roles:['administrator','staff'] },
  { key:'stockin',   label:'Stock In',          href:'stock-in.html',          icon:'stockIn',   roles:['administrator','staff'] },
  { key:'stockout',  label:'Stock Out',         href:'stock-out.html',         icon:'stockOut',  roles:['administrator','staff'] },
  { key:'history',   label:'Inventory History', href:'inventory-history.html', icon:'history',   roles:['administrator','staff'] },
  { key:'reports',   label:'Reports',           href:'reports.html',           icon:'reports',   roles:['administrator','staff'] },
  { key:'users',     label:'Users',             href:'users.html',             icon:'users',     roles:['administrator'] },
  { key:'activity',  label:'Activity Logs',     href:'activity-logs.html',     icon:'activity',  roles:['administrator'] },
  { key:'settings',  label:'Settings',          href:'settings.html',          icon:'settings',  roles:['administrator'] },
];

// Notifications now come from the API, so they fill in after the shell is drawn.
async function loadNotifications(panel){
  const [lowStock, allTx] = await Promise.all([getLowStockProducts(3), listTransactions()]);
  const transactions = allTx.slice(0, 2);
  const notificationCount = lowStock.length + transactions.length;
  const items = [
    ...lowStock.map(product => ({
      tone: product.stock === 0 ? 'danger' : 'warning',
      icon: product.stock === 0 ? ICONS.xCircle : ICONS.alertTriangle,
      title: product.stock === 0 ? 'Out of stock' : 'Low stock level',
      detail: `${esc(product.name)} · ${product.stock} left`,
      href: `products.html?product=${encodeURIComponent(product.id)}`,
      time: 'Needs attention'
    })),
    ...transactions.map(t => ({
      tone: t.type === 'IN' ? 'success' : 'primary',
      icon: t.type === 'IN' ? ICONS.stockIn : ICONS.stockOut,
      title: t.type === 'IN' ? 'Stock received' : 'Stock dispatched',
      detail: `${esc(t.productName || 'Inventory item')} · ${t.qty} units`,
      href: t.productName ? `products.html?product=${encodeURIComponent(t.productId)}` : '',
      time: formatTimeAgo(t.at)
    }))
  ];
  panel.innerHTML = `
    <div class="notification-header">
      <div>
        <p class="notification-title">Notifications</p>
        <p class="notification-subtitle">${notificationCount ? `${notificationCount} updates to review` : 'You are all caught up'}</p>
      </div>
      <span class="notification-count">${notificationCount}</span>
    </div>
    <div class="notification-list">
      ${items.length ? items.map(item => `
        <a class="notification-item" href="${item.href || '#'}">
          <span class="notification-icon ${item.tone}">${item.icon}</span>
          <span class="notification-copy"><b>${item.title}</b><span>${item.detail}</span></span>
          <span class="notification-time">${item.time}</span>
        </a>
      `).join('') : '<div class="notification-empty">No new inventory activity.</div>'}
    </div>
    <a class="notification-footer" href="dashboard.html">Open dashboard ${ICONS.arrowUpRight}</a>
  `;
}

function renderLayout(opts){
  const session = opts.session;
  const activeKey = opts.activeKey;
  const shell = document.querySelector('.app-shell');

  const navHtml = NAV_ITEMS
    .filter(item => item.roles.includes(session.role))
    .map(item => `
      <a class="nav-item ${item.key === activeKey ? 'active' : ''}" href="${item.href}">
        ${ICONS[item.icon]}
        <span>${item.label}</span>
      </a>
    `).join('');

  document.getElementById('sidebar').innerHTML = `
    <div>
      <div class="sidebar-header">
        <div class="sidebar-logo">${ICONS.logo}</div>
        <div>
          <div class="sidebar-brand-name">StockTrack</div>
          <div class="sidebar-brand-sub">Inventory System</div>
        </div>
        <button class="sidebar-close" id="sidebarClose" title="Close sidebar" aria-label="Close sidebar">${ICONS.close}</button>
      </div>
      <nav class="sidebar-nav">${navHtml}</nav>
    </div>
    <div class="sidebar-footer">
      <a href="#" class="nav-item" id="logoutBtn">${ICONS.logout}<span>Log Out</span></a>
    </div>
  `;

  document.getElementById('topbar').innerHTML = `
    <div class="topbar-title"><button class="sidebar-open" id="sidebarOpen" title="Open sidebar" aria-label="Open sidebar">${ICONS.menu}</button><div class="topbar-brand"><div class="sidebar-logo">${ICONS.logo}</div><div><div class="sidebar-brand-name">StockTrack</div><div class="sidebar-brand-sub">Inventory System</div></div></div></div>
    <div class="top-actions">
      <div class="notification-wrap">
        <button class="icon-btn notification-trigger" id="notificationToggle" title="Notifications" aria-label="Open notifications" aria-expanded="false">
          ${ICONS.bell}<span class="notif-dot"></span>
        </button>
        <div class="notification-panel" id="notificationPanel" aria-hidden="true"></div>
      </div>
      <div class="user-profile" id="profileToggle">
        <div class="avatar">${esc(initials(session.name))}</div>
        <div class="profile-meta">
          <p>${esc(session.name)}</p>
          <p>${ROLE_LABELS[session.role]}</p>
        </div>
        ${ICONS.chevronDown}
        <div class="profile-dropdown" id="profileDropdown">
          <div style="padding:10px 14px; font-size:12px; color:var(--text-light); border-bottom:1px solid var(--border-light);">
            Signed in as <b>${esc(session.username)}</b>
          </div>
          <button id="dropdownLogout">Log Out</button>
        </div>
      </div>
    </div>
  `;

  document.getElementById('sidebarClose').addEventListener('click', ()=>{
    shell.classList.add('sidebar-collapsed');
    document.getElementById('sidebarOpen').focus();
  });
  document.getElementById('sidebarOpen').addEventListener('click', ()=>{
    shell.classList.remove('sidebar-collapsed');
    document.getElementById('sidebarClose').focus();
  });

  document.getElementById('logoutBtn').addEventListener('click', (e)=>{ e.preventDefault(); logout(); });
  document.getElementById('dropdownLogout').addEventListener('click', logout);
  const toggle = document.getElementById('profileToggle');
  const dropdown = document.getElementById('profileDropdown');
  toggle.addEventListener('click', (e)=>{
    e.stopPropagation();
    dropdown.classList.toggle('open');
  });
  document.addEventListener('click', ()=> dropdown.classList.remove('open'));

  const notificationToggle = document.getElementById('notificationToggle');
  const notificationPanel = document.getElementById('notificationPanel');
  notificationPanel.innerHTML = '<div class="notification-empty">Loading…</div>';
  loadNotifications(notificationPanel).catch(()=>{
    notificationPanel.innerHTML = '<div class="notification-empty">Could not load notifications.</div>';
  });
  notificationToggle.addEventListener('click', (e)=>{
    e.stopPropagation();
    const isOpen = notificationPanel.classList.toggle('open');
    notificationToggle.setAttribute('aria-expanded', String(isOpen));
    notificationPanel.setAttribute('aria-hidden', String(!isOpen));
  });
  notificationPanel.addEventListener('click', e => e.stopPropagation());
  document.addEventListener('click', ()=>{
    notificationPanel.classList.remove('open');
    notificationToggle.setAttribute('aria-expanded', 'false');
    notificationPanel.setAttribute('aria-hidden', 'true');
  });
}

function showToast(message, type){
  let el = document.getElementById('toast');
  if(!el){
    el = document.createElement('div');
    el.id = 'toast';
    el.className = 'toast';
    document.body.appendChild(el);
  }
  const icon = type === 'error' ? ICONS.xCircle : type === 'success' ? ICONS.stockIn : ICONS.bell;
  el.innerHTML = `<span class="toast-icon">${icon}</span><span>${esc(message)}</span><button class="toast-close" aria-label="Dismiss notification">&times;</button>`;
  el.querySelector('.toast-close').onclick = ()=> el.classList.remove('show');
  el.className = 'toast show' + (type ? ' ' + type : '');
  clearTimeout(el._t);
  el._t = setTimeout(()=> el.classList.remove('show'), 2600);
}
