const money = value => new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(value);
const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
const state = { products: [], knownProducts: new Map(), cart: JSON.parse(localStorage.getItem('bps-cart') || localStorage.getItem('marketly-cart') || '{}'), sort: '', query: '', category: '', token: sessionStorage.getItem('bps-token'), customer: null, registering: false, checkoutSaga: null };
const $ = id => document.getElementById(id);
const grid = $('productGrid'), modal = $('modalWrap');

async function api(path, options = {}) {
  const headers = new Headers(options.headers || {});
  if (options.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  if (state.token) headers.set('Authorization', `Bearer ${state.token}`);
  const response = await fetch(path, { ...options, headers });
  const text = await response.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { data = text; }
  if (!response.ok) throw new Error(data?.message || data?.title || `Request failed (${response.status})`);
  return { data, status: response.status };
}
function toast(message) { let el = document.querySelector('.toast'); if (!el) { el = document.createElement('div'); el.className = 'toast'; document.body.append(el); } el.textContent = message; el.classList.add('show'); setTimeout(() => el.classList.remove('show'), 2800); }
function renderProducts() {
  grid.innerHTML = state.products.map(p => `<article class="product-card"><div class="product-image"><img loading="lazy" src="${esc(p.imageUrl || '')}" alt="${esc(p.name)}"><span class="badge">${esc(p.category)}</span><button class="wish" aria-label="Save ${esc(p.name)}">♡</button></div><div class="product-info"><div class="product-kicker">${esc(p.category)} / BPS FINDS</div><h3>${esc(p.name)}</h3><p class="product-description">${esc(p.description)}</p><div class="price-row"><span class="price">${money(p.price)}</span></div><small>${p.inventory > 0 ? `${p.inventory} available` : 'Currently unavailable'}</small><button class="add-btn" data-add="${esc(p.id)}" ${p.inventory < 1 ? 'disabled' : ''} aria-label="Add ${esc(p.name)} to cart">+</button></div></article>`).join('');
  $('emptyState').hidden = state.products.length > 0;
}
async function loadProducts() {
  grid.innerHTML = '<p class="loading-state">Finding good things…</p>';
  const params = new URLSearchParams({ pageSize: '50' });
  if (state.query) params.set('search', state.query);
  if (state.category) params.set('category', state.category);
  if (state.sort) params.set('sort', state.sort);
  try { const { data } = await api(`/api/catalog/products?${params}`); state.products = data.items || []; for (const product of state.products) state.knownProducts.set(product.id, product); renderProducts(); renderCart(); }
  catch (e) { state.products = []; grid.innerHTML = `<p class="loading-state">${esc(e.message)}. Check that the local services are running.</p>`; $('emptyState').hidden = true; }
}
function persistCart() { localStorage.setItem('bps-cart', JSON.stringify(state.cart)); localStorage.removeItem('marketly-cart'); renderCart(); }
function renderCart() {
  const rows = Object.entries(state.cart).map(([id, qty]) => ({ product: state.knownProducts.get(id), qty: Number(qty) })).filter(x => x.product && x.qty > 0);
  const total = rows.reduce((sum, row) => sum + row.product.price * row.qty, 0), quantity = rows.reduce((sum, row) => sum + row.qty, 0);
  $('cartCount').textContent = $('drawerCount').textContent = quantity;
  $('cartSubtotal').textContent = money(total);
  $('cartEmpty').style.display = rows.length ? 'none' : 'flex'; $('cartFoot').style.display = rows.length ? 'block' : 'none';
  $('cartItems').innerHTML = rows.map(({ product: p, qty }) => `<div class="cart-row"><img src="${esc(p.imageUrl || '')}" alt=""><div><h3>${esc(p.name)}</h3><small>${esc(p.category)} · ${money(p.price)}</small><div class="qty"><button data-qty="${p.id}" data-delta="-1">−</button><span>${qty}</span><button data-qty="${p.id}" data-delta="1">+</button></div></div><span class="line-price">${money(p.price * qty)}</span></div>`).join('');
}
function openCart() { $('cartDrawer').classList.add('open'); $('scrim').classList.add('open'); }
function closeCart() { $('cartDrawer').classList.remove('open'); $('scrim').classList.remove('open'); }
function showAccount() {
  $('accountForm').hidden = false; $('checkoutForm').hidden = true; $('firstNameField').hidden = !state.registering;
  $('modalTitle').innerHTML = state.registering ? 'Join the <em>good finds.</em>' : 'Welcome <em>back.</em>';
  $('modalText').textContent = state.registering ? 'Create an account to check out securely.' : 'Sign in to continue securely.';
  $('accountSubmit').textContent = state.registering ? 'Create account' : 'Sign in'; $('accountModeToggle').textContent = state.registering ? 'I already have an account' : 'Create an account';
  $('passwordInput').autocomplete = state.registering ? 'new-password' : 'current-password'; $('demoNote').textContent = 'Your password is sent only to the Identity API over the local API proxy.';
  modal.classList.add('open');
}
function showCheckout() {
  $('accountForm').hidden = true; $('checkoutForm').hidden = false; $('modalTitle').innerHTML = 'Delivery <em>details.</em>';
  $('modalText').textContent = 'Stock is reserved for 15 minutes while you complete payment.';
  const user = state.customer; if (user && !$('deliveryName').value) $('deliveryName').value = user.firstName || '';
  const subtotal = Object.entries(state.cart).reduce((s, [id, qty]) => s + (state.knownProducts.get(id)?.price || 0) * Number(qty), 0);
  $('checkoutTotal').textContent = money(subtotal + (subtotal >= 500 || subtotal === 0 ? 0 : 60) + subtotal * 0.18);
  $('demoNote').textContent = 'Payments use Razorpay test mode when configured; otherwise local simulated payment is enabled.';
  modal.classList.add('open');
}
async function loadRazorpay() {
  if (window.Razorpay) return;
  await new Promise((resolve, reject) => { const script = document.createElement('script'); script.src = 'https://checkout.razorpay.com/v1/checkout.js'; script.onload = resolve; script.onerror = () => reject(new Error('Unable to load Razorpay Checkout.')); document.head.append(script); });
}
async function confirmUntilComplete(path, options) {
  for (let attempt = 0; attempt < 8; attempt++) {
    const { data, status } = await api(path, options);
    if (status !== 202 && data?.id) return data;
    await new Promise(resolve => setTimeout(resolve, 750));
  }
  throw new Error('Payment was received and checkout is still processing. Retry safely; the order is idempotent.');
}
async function startPayment(result) {
  const { sagaId, payment } = result;
  if (!payment) throw new Error('Checkout did not return payment details. Retry using the same cart.');
  state.checkoutSaga = sagaId;
  if (payment.provider === 'Simulated') {
    const success = confirm(`Local payment simulation\n\nCharge ${money(payment.amountSubunits / 100)} to the demo checkout? No real payment will be made.`);
    if (!success) return;
    const data = await confirmUntilComplete(`/api/orders/checkout/${sagaId}/simulate-payment`, { method: 'POST' });
    return finishOrder(data);
  }
  await loadRazorpay();
  const checkout = new Razorpay({ key: payment.publicKeyId, order_id: payment.providerOrderId, amount: payment.amountSubunits, currency: payment.currency, name: 'Bhagyashree Pattar Store', description: 'BPS order', prefill: { name: $('deliveryName').value, email: $('emailInput').value }, theme: { color: '#315e53' }, handler: async response => {
    try { const data = await confirmUntilComplete(`/api/orders/checkout/${sagaId}/payment`, { method: 'POST', body: JSON.stringify({ razorpayOrderId: response.razorpay_order_id, razorpayPaymentId: response.razorpay_payment_id, razorpaySignature: response.razorpay_signature }) }); finishOrder(data); }
    catch (e) { toast(`Payment verification: ${e.message}`); }
  } });
  checkout.on('payment.failed', response => toast(response.error?.description || 'Payment did not complete. Your reservation remains active briefly.'));
  checkout.open();
}
function finishOrder(order) {
  state.cart = {}; persistCart(); sessionStorage.removeItem('bps-checkout-key'); sessionStorage.removeItem('bps-checkout-hash');
  modal.classList.remove('open'); toast(`Order ${String(order.id).slice(0, 8)} placed. Total ${money(order.total)}.`);
}

grid.addEventListener('click', e => { const button = e.target.closest('[data-add]'); if (!button) return; const id = button.dataset.add; state.cart[id] = (Number(state.cart[id]) || 0) + 1; persistCart(); toast('Added to your cart.'); });
$('cartItems').addEventListener('click', e => { const b = e.target.closest('[data-qty]'); if (!b) return; const id = b.dataset.qty; state.cart[id] = (Number(state.cart[id]) || 0) + Number(b.dataset.delta); if (state.cart[id] <= 0) delete state.cart[id]; persistCart(); });
$('cartOpen').onclick = openCart; $('cartClose').onclick = closeCart; $('keepShopping').onclick = closeCart; $('scrim').onclick = closeCart;
document.querySelectorAll('.filter').forEach(b => b.onclick = () => { document.querySelectorAll('.filter').forEach(x => x.classList.toggle('active', x === b)); state.sort = b.dataset.filter === 'low' ? 'price-asc' : b.dataset.filter === 'high' ? 'price-desc' : ''; loadProducts(); });
document.querySelectorAll('.category-tile').forEach(b => b.onclick = () => { state.category = b.dataset.category; $('categorySelect').value = state.category; document.getElementById('popular').scrollIntoView({ behavior: 'smooth' }); loadProducts(); });
$('searchForm').onsubmit = e => { e.preventDefault(); state.query = $('searchInput').value.trim(); state.category = $('categorySelect').value === 'all' ? '' : $('categorySelect').value; $('popular').scrollIntoView({ behavior: 'smooth' }); loadProducts(); };
$('categorySelect').onchange = () => { state.category = $('categorySelect').value === 'all' ? '' : $('categorySelect').value; loadProducts(); };
$('clearFilters').onclick = () => { state.query = ''; state.category = ''; state.sort = ''; $('searchInput').value = ''; $('categorySelect').value = 'all'; document.querySelectorAll('.filter').forEach(x => x.classList.toggle('active', x.dataset.filter === 'all')); loadProducts(); };
$('checkoutBtn').onclick = () => { closeCart(); if (!state.token) { state.registering = false; showAccount(); } else showCheckout(); };
$('accountBtn').onclick = () => { state.registering = false; showAccount(); };
$('accountModeToggle').onclick = () => { state.registering = !state.registering; showAccount(); };
$('accountForm').onsubmit = async e => {
  e.preventDefault(); const button = $('accountSubmit'); button.disabled = true;
  try {
    const request = state.registering ? { firstName: $('firstNameInput').value.trim(), email: $('emailInput').value.trim(), password: $('passwordInput').value } : { email: $('emailInput').value.trim(), password: $('passwordInput').value };
    const { data } = await api(state.registering ? '/api/identity/auth/register' : '/api/identity/auth/login', { method: 'POST', body: JSON.stringify(request) });
    state.token = data.accessToken; state.customer = data.customer; sessionStorage.setItem('bps-token', state.token); $('accountBtn').innerHTML = `<span class="account-icon">♙</span><span><small>Hello, ${esc(data.customer.firstName)}</small><b>Account & Lists</b></span>`;
    if (Object.keys(state.cart).length) showCheckout(); else modal.classList.remove('open');
  } catch (err) { $('modalText').textContent = err.message; } finally { button.disabled = false; }
};
$('checkoutForm').onsubmit = async e => {
  e.preventDefault(); const button = $('placeOrderBtn'); button.disabled = true; button.textContent = 'Reserving your items…';
  const body = { items: Object.entries(state.cart).filter(([, q]) => q > 0).map(([productId, quantity]) => ({ productId, quantity })), deliveryAddress: { name: $('deliveryName').value.trim(), addressLine1: $('addressLine1').value.trim(), addressLine2: $('addressLine2').value.trim(), city: $('cityInput').value.trim(), region: $('regionInput').value.trim(), postalCode: $('postalInput').value.trim(), countryCode: $('countryInput').value.trim().toUpperCase() } };
  const serialized = JSON.stringify(body); let key = sessionStorage.getItem('bps-checkout-key');
  if (sessionStorage.getItem('bps-checkout-hash') !== serialized || !key) { key = crypto.randomUUID(); sessionStorage.setItem('bps-checkout-key', key); sessionStorage.setItem('bps-checkout-hash', serialized); }
  try {
    const { data, status } = await api('/api/orders/checkout', { method: 'POST', headers: { 'Idempotency-Key': key }, body: serialized });
    if (status === 202) { await startPayment(data); return; }
    finishOrder(data);
  } catch (err) { $('modalText').textContent = err.message; } finally { button.disabled = false; button.textContent = 'Continue to payment'; }
};
$('modalClose').onclick = () => modal.classList.remove('open'); modal.addEventListener('click', e => { if (e.target === modal) modal.classList.remove('open'); });
$('newsletterForm').onsubmit = e => { e.preventDefault(); $('newsletterMsg').textContent = 'You’re on the list. Look out for a little something soon!'; e.target.reset(); };
$('menuBtn').onclick = () => $('categories').scrollIntoView({ behavior: 'smooth' });
async function init() {
  await loadProducts();
  renderCart();
  if (state.token) { try { const { data } = await api('/api/identity/auth/me'); state.customer = data; $('accountBtn').innerHTML = `<span class="account-icon">♙</span><span><small>Hello, ${esc(data.firstName)}</small><b>Account & Lists</b></span>`; } catch { state.token = null; sessionStorage.removeItem('bps-token'); } }
}
init();
