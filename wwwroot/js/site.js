/**
 * TAM THÁI TỬ SPORT — Main JavaScript
 * Cart, Toast, Search, Navbar, Wishlist
 */

// ─── UTILITY ─────────────────────────────────────────────
const $ = (sel, ctx = document) => ctx.querySelector(sel);
const $$ = (sel, ctx = document) => [...ctx.querySelectorAll(sel)];
const fmt = n => new Intl.NumberFormat('vi-VN').format(n) + '₫';

// ─── TOAST SYSTEM ─────────────────────────────────────────
const Toast = {
    container: null,
    init() {
        this.container = document.getElementById('toastContainer');
    },
    show(message, type = 'success', duration = 3500) {
        if (!this.container) return;
        const icons = { success: '✅', error: '❌', warning: '⚠️', info: 'ℹ️' };
        const toast = document.createElement('div');
        toast.className = `toast ${type}`;
        toast.setAttribute('role', 'alert');
        toast.innerHTML = `
            <span class="toast-icon" aria-hidden="true">${icons[type] || icons.info}</span>
            <span class="toast-text">${message}</span>
            <button onclick="this.parentElement.remove()" style="background:none;border:none;cursor:pointer;color:var(--text-muted);font-size:1rem;padding:0 0 0 0.5rem;" aria-label="Đóng">✕</button>
        `;
        this.container.appendChild(toast);
        setTimeout(() => {
            toast.style.animation = 'toastIn 0.3s reverse forwards';
            setTimeout(() => toast.remove(), 300);
        }, duration);
    },
    success: (msg) => Toast.show(msg, 'success'),
    error: (msg) => Toast.show(msg, 'error'),
    warning: (msg) => Toast.show(msg, 'warning'),
    info: (msg) => Toast.show(msg, 'info'),
};

// ─── CART SYSTEM ──────────────────────────────────────────
const Cart = {
    items: [],

    init() {
        this.load();
        this.bindEvents();
        this.render();
    },

    load() {
        try {
            const raw = JSON.parse(localStorage.getItem('ttt_cart') || '[]');
            this.items = raw.map(item => ({
                ...item,
                id: String(item.id),
                size: item.size || '',
                color: item.color || '',
            }));
        } catch {
            this.items = [];
        }
    },

    save() {
        localStorage.setItem('ttt_cart', JSON.stringify(this.items));
    },

    add(product) {
        product.id = String(product.id);
        product.size = product.size || '';
        product.color = product.color || '';

        const existing = this.items.find(i =>
            i.id === product.id && i.size === product.size && i.color === product.color
        );
        if (existing) {
            existing.qty = Math.min(existing.qty + (product.qty || 1), 99);
        } else {
            this.items.push({ ...product, qty: product.qty || 1 });
        }
        this.save();
        this.render();
        Toast.success(`✅ Đã thêm <strong>${product.name}</strong> vào giỏ hàng!`);
        this.openSidebar();
    },

    remove(id, size, color) {
        id = String(id);
        size = size || '';
        color = color || '';
        this.items = this.items.filter(i => !(i.id === id && i.size === size && i.color === color));
        this.save();
        this.render();
    },

    updateQty(id, size, color, qty) {
        id = String(id);
        size = size || '';
        color = color || '';
        if (qty <= 0) {
            this.remove(id, size, color);
            return;
        }
        const item = this.items.find(i => i.id === id && i.size === size && i.color === color);
        if (item) {
            item.qty = Math.min(qty, 99);
            this.save();
            this.render();
        }
    },

    clear() {
        this.items = [];
        this.save();
        this.render();
    },

    get count() {
        return this.items.reduce((s, i) => s + i.qty, 0);
    },

    get subtotal() {
        return this.items.reduce((s, i) => s + i.price * i.qty, 0);
    },

    render() {
        const countEl = document.getElementById('cartCount');
        const miniCountEl = document.getElementById('miniCartCount');
        const bodyEl = document.getElementById('miniCartBody');
        const footerEl = document.getElementById('miniCartFooter');
        const subtotalEl = document.getElementById('cartSubtotal');
        const totalEl = document.getElementById('cartTotal');

        const count = this.count;
        if (countEl) countEl.textContent = count;
        if (miniCountEl) miniCountEl.textContent = count;

        if (!bodyEl) return;

        if (this.items.length === 0) {
            bodyEl.innerHTML = `
                <div class="mini-cart-empty">
                    <div class="mini-cart-empty-icon">🛒</div>
                    <div class="mini-cart-empty-text">Giỏ hàng trống</div>
                    <p style="font-size:0.8rem;color:var(--text-muted);margin-top:0.5rem;">Hãy thêm sản phẩm vào giỏ hàng</p>
                </div>`;
            if (footerEl) footerEl.style.display = 'none';
            return;
        }

        bodyEl.innerHTML = this.items.map(item => `
            <div class="mini-cart-item" data-id="${item.id}" data-size="${item.size || ''}" data-color="${item.color || ''}">
                <img class="mini-cart-item-img"
                     src="${item.image ? '/uploads/products/' + item.image : '/img/no-image.png'}"
                     alt="${item.name}"
                     loading="lazy"
                     onerror="this.src='/img/no-image.png'">
                <div class="mini-cart-item-info">
                    <div class="mini-cart-item-name">${item.name}</div>
                    <div class="mini-cart-item-meta">
                        ${item.size ? `Size: <strong>${item.size}</strong>` : ''}
                        ${item.size && item.color ? ' • ' : ''}
                        ${item.color ? `Màu: <strong>${item.color}</strong>` : ''}
                    </div>
                    <div class="mini-cart-item-controls">
                        <div class="qty-control">
                            <button class="qty-btn" onclick="Cart.updateQty('${item.id}','${item.size||''}','${item.color||''}',${item.qty - 1})" aria-label="Giảm số lượng">−</button>
                            <span class="qty-value">${item.qty}</span>
                            <button class="qty-btn" onclick="Cart.updateQty('${item.id}','${item.size||''}','${item.color||''}',${item.qty + 1})" aria-label="Tăng số lượng">+</button>
                        </div>
                        <span class="mini-cart-item-price">${fmt(item.price * item.qty)}</span>
                        <div style="display:flex;align-items:center;gap:0.4rem;">
                            <button class="mini-cart-item-edit" onclick="Cart.editOption('${item.id}','${item.size||''}','${item.color||''}')" aria-label="Chỉnh sửa lựa chọn" style="background:none;border:none;cursor:pointer;color:var(--text-muted);font-size:1.25rem;transition:var(--transition);display:flex;align-items:center;padding:0.25rem;">
                                <i class='bx bx-edit-alt'></i>
                            </button>
                            <button class="mini-cart-item-remove" onclick="Cart.remove('${item.id}','${item.size||''}','${item.color||''}')" aria-label="Xóa sản phẩm">
                                <i class='bx bx-trash'></i>
                            </button>
                        </div>
                    </div>
                </div>
            </div>`).join('');

        if (subtotalEl) subtotalEl.textContent = fmt(this.subtotal);
        if (totalEl) totalEl.textContent = fmt(this.subtotal);
        if (footerEl) footerEl.style.display = 'block';
        window.dispatchEvent(new CustomEvent('cart-updated'));
    },

    openSidebar() {
        const overlay = document.getElementById('cartOverlay');
        const sidebar = document.getElementById('miniCart');
        const btn = document.getElementById('cartToggleBtn');
        overlay?.classList.add('open');
        sidebar?.classList.add('open');
        btn?.setAttribute('aria-expanded', 'true');
        document.body.style.overflow = 'hidden';
    },

    closeSidebar() {
        const overlay = document.getElementById('cartOverlay');
        const sidebar = document.getElementById('miniCart');
        const btn = document.getElementById('cartToggleBtn');
        overlay?.classList.remove('open');
        sidebar?.classList.remove('open');
        btn?.setAttribute('aria-expanded', 'false');
        document.body.style.overflow = '';
    },

    bindEvents() {
        document.getElementById('cartToggleBtn')?.addEventListener('click', () => this.openSidebar());
        document.getElementById('cartCloseBtn')?.addEventListener('click', () => this.closeSidebar());
        document.getElementById('cartOverlay')?.addEventListener('click', () => this.closeSidebar());

        // Escape key
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape') this.closeSidebar();
        });
    },

    editOption(id, oldSize, oldColor) {
        id = String(id);
        oldSize = oldSize || '';
        oldColor = oldColor || '';
        
        const item = this.items.find(i => i.id === id && i.size === oldSize && i.color === oldColor);
        if (!item) return;

        fetch(`/api/products/${id}/options`)
            .then(res => {
                if (!res.ok) throw new Error();
                return res.json();
            })
            .then(data => {
                const sizes = data.sizes ? data.sizes.split(',').map(s => s.trim()).filter(s => s) : [];
                const colors = data.colors ? data.colors.split(',').map(c => c.trim()).filter(c => c) : [];
                this.showEditModal(item, sizes, colors);
            })
            .catch(() => {
                Toast.error('❌ Không thể tải thông tin sản phẩm này. Thử lại sau.');
            });
    },

    showEditModal(item, sizes, colors) {
        document.getElementById('cartEditModal')?.remove();

        let selectedSize = item.size;
        let selectedColor = item.color;

        const modal = document.createElement('div');
        modal.id = 'cartEditModal';
        modal.className = 'cart-edit-modal-overlay';
        modal.innerHTML = `
            <div class="cart-edit-modal">
                <div class="cart-edit-modal-header">
                    <h3>Chỉnh sửa lựa chọn</h3>
                    <button class="cart-edit-modal-close" onclick="document.getElementById('cartEditModal').remove()">&times;</button>
                </div>
                <div class="cart-edit-modal-body">
                    <div class="cart-edit-modal-product">
                        <img src="${item.image ? '/uploads/products/' + item.image : '/img/no-image.png'}" onerror="this.src='/img/no-image.png'">
                        <div>
                            <div class="product-name" style="font-size:0.9rem;font-weight:600;color:var(--text-primary);margin-bottom:0.25rem;">${item.name}</div>
                            <div class="product-price" style="font-size:0.85rem;font-weight:700;color:var(--primary);">${fmt(item.price)}</div>
                        </div>
                    </div>
                    
                    ${sizes.length ? `
                    <div class="option-group" style="margin-bottom:1.25rem;">
                        <div class="option-label" style="font-size:0.82rem;font-weight:600;color:var(--text-secondary);margin-bottom:0.5rem;">Chọn Size:</div>
                        <div class="option-pills" id="modalSizePills">
                            ${sizes.map(s => `
                                <button type="button" class="option-pill ${s === selectedSize ? 'selected' : ''}" data-val="${s}">
                                    ${s}
                                </button>
                            `).join('')}
                        </div>
                    </div>
                    ` : ''}

                    ${colors.length ? `
                    <div class="option-group" style="margin-bottom:1.25rem;">
                        <div class="option-label" style="font-size:0.82rem;font-weight:600;color:var(--text-secondary);margin-bottom:0.5rem;">Chọn Màu sắc:</div>
                        <div class="option-pills" id="modalColorPills">
                            ${colors.map(c => `
                                <button type="button" class="option-pill ${c === selectedColor ? 'selected' : ''}" data-val="${c}">
                                    ${c}
                                </button>
                            `).join('')}
                        </div>
                    </div>
                    ` : ''}
                </div>
                <div class="cart-edit-modal-footer">
                    <button class="btn btn-secondary" style="background:var(--bg-surface);border:1px solid var(--border);color:var(--text-secondary);padding:0.5rem 1.25rem;border-radius:var(--radius);font-weight:600;cursor:pointer;font-size:0.85rem;" onclick="document.getElementById('cartEditModal').remove()">Hủy</button>
                    <button class="btn btn-primary" style="background:var(--gradient-brand);color:white;border:none;padding:0.5rem 1.25rem;border-radius:var(--radius);font-weight:700;cursor:pointer;font-size:0.85rem;box-shadow:var(--shadow-glow);" id="modalUpdateBtn">Cập nhật</button>
                </div>
            </div>
        `;
        document.body.appendChild(modal);

        modal.querySelectorAll('#modalSizePills .option-pill').forEach(btn => {
            btn.addEventListener('click', () => {
                modal.querySelectorAll('#modalSizePills .option-pill').forEach(b => b.classList.remove('selected'));
                btn.classList.add('selected');
                selectedSize = btn.dataset.val;
            });
        });

        modal.querySelectorAll('#modalColorPills .option-pill').forEach(btn => {
            btn.addEventListener('click', () => {
                modal.querySelectorAll('#modalColorPills .option-pill').forEach(b => b.classList.remove('selected'));
                btn.classList.add('selected');
                selectedColor = btn.dataset.val;
            });
        });

        document.getElementById('modalUpdateBtn').addEventListener('click', () => {
            this.updateItemOptions(item.id, item.size, item.color, selectedSize, selectedColor);
            modal.remove();
        });
    },

    updateItemOptions(id, oldSize, oldColor, newSize, newColor) {
        id = String(id);
        oldSize = oldSize || '';
        oldColor = oldColor || '';
        newSize = newSize || '';
        newColor = newColor || '';

        if (oldSize === newSize && oldColor === newColor) return;

        const origIdx = this.items.findIndex(i => i.id === id && i.size === oldSize && i.color === oldColor);
        if (origIdx === -1) return;

        const origItem = this.items[origIdx];
        const existIdx = this.items.findIndex(i => i.id === id && i.size === newSize && i.color === newColor);

        if (existIdx !== -1 && existIdx !== origIdx) {
            this.items[existIdx].qty = Math.min(this.items[existIdx].qty + origItem.qty, 99);
            this.items.splice(origIdx, 1);
        } else {
            origItem.size = newSize;
            origItem.color = newColor;
        }

        this.save();
        this.render();
        Toast.success('✅ Đã cập nhật lựa chọn sản phẩm!');
    },

    // Helpers used by Cart page and Checkout views
    getItems() { return this.items; },
    saveItems(items) { this.items = items; this.save(); this.render(); }
};

// Global badge updater (used by OrderSuccess after cart.clear)
function updateCartBadge() {
    const countEl = document.getElementById('cartCount');
    const miniCountEl = document.getElementById('miniCartCount');
    const count = Cart.count;
    if (countEl) countEl.textContent = count;
    if (miniCountEl) miniCountEl.textContent = count;
}

// ─── WISHLIST SYSTEM ──────────────────────────────────────
const Wishlist = {
    items: new Set(),

    init() {
        try {
            const saved = JSON.parse(localStorage.getItem('ttt_wishlist') || '[]');
            this.items = new Set(saved);
        } catch {
            this.items = new Set();
        }
        this.updateButtons();
        this.updateCount();
    },

    toggle(id, name) {
        if (this.items.has(id)) {
            this.items.delete(id);
            Toast.info(`💔 Đã xóa <strong>${name}</strong> khỏi danh sách yêu thích`);
        } else {
            this.items.add(id);
            Toast.success(`❤️ Đã thêm <strong>${name}</strong> vào danh sách yêu thích!`);
        }
        localStorage.setItem('ttt_wishlist', JSON.stringify([...this.items]));
        this.updateButtons();
        this.updateCount();
    },

    updateButtons() {
        $$('[data-wishlist-id]').forEach(btn => {
            const id = btn.dataset.wishlistId;
            const active = this.items.has(id);
            btn.classList.toggle('active', active);
            btn.setAttribute('aria-pressed', active);
            const icon = btn.querySelector('i');
            if (icon) icon.className = active ? 'bx bxs-heart' : 'bx bx-heart';
        });
    },

    updateCount() {
        const countEl = document.getElementById('wishlistCount');
        if (countEl) {
            const count = this.items.size;
            countEl.textContent = count;
            countEl.style.display = count > 0 ? 'flex' : 'none';
        }
    }
};

// ─── SEARCH AUTOCOMPLETE ─────────────────────────────────
const Search = {
    input: null,
    results: null,
    timeout: null,

    init() {
        this.input = document.getElementById('searchInput');
        this.results = document.getElementById('searchResults');
        if (!this.input || !this.results) return;
        this.bindEvents();
    },

    bindEvents() {
        this.input.addEventListener('input', () => {
            clearTimeout(this.timeout);
            const q = this.input.value.trim();
            if (q.length < 2) {
                this.results.style.display = 'none';
                return;
            }
            this.timeout = setTimeout(() => this.fetch(q), 300);
        });

        this.input.addEventListener('focus', () => {
            if (this.input.value.trim().length >= 2) {
                this.results.style.display = 'block';
            }
        });

        document.addEventListener('click', e => {
            if (!e.target.closest('.navbar-search')) {
                this.results.style.display = 'none';
            }
        });
    },

    async fetch(q) {
        try {
            const res = await fetch(`/api/products/search?q=${encodeURIComponent(q)}&limit=5`);
            if (!res.ok) return;
            const data = await res.json();
            this.renderResults(data, q);
        } catch {
            this.results.style.display = 'none';
        }
    },

    renderResults(items, q) {
        if (!items.length) {
            this.results.innerHTML = `<div class="search-result-item" style="color:var(--text-muted)">Không tìm thấy sản phẩm cho "<strong>${q}</strong>"</div>`;
        } else {
            this.results.innerHTML = items.map(p => `
                <a href="/san-pham/${p.slug}" class="search-result-item" role="option">
                    <img class="search-result-img" src="${p.image || '/img/no-image.png'}" alt="${p.name}" loading="lazy" onerror="this.src='/img/no-image.png'" />
                    <div>
                        <div class="search-result-name">${p.name}</div>
                        <div class="search-result-price">${fmt(p.finalPrice)}</div>
                    </div>
                </a>`).join('') +
                `<a href="/san-pham?q=${encodeURIComponent(q)}" class="search-result-item" style="justify-content:center;color:var(--primary);font-weight:600;font-size:0.82rem;">
                    Xem tất cả kết quả cho "${q}" →
                </a>`;
        }
        this.results.style.display = 'block';
    }
};

// ─── NAVBAR SCROLL ────────────────────────────────────────
const Navbar = {
    init() {
        const nav = document.getElementById('mainNav');
        if (!nav) return;

        let last = 0;
        window.addEventListener('scroll', () => {
            const y = window.scrollY;
            nav.classList.toggle('scrolled', y > 50);
            last = y;
        }, { passive: true });

        // Mobile menu
        const toggle = document.getElementById('mobileMenuToggle');
        const menu = document.getElementById('mobileMenu');
        const icon = document.getElementById('menuIcon');

        toggle?.addEventListener('click', () => {
            const isOpen = menu?.classList.toggle('open');
            if (icon) icon.className = isOpen ? 'bx bx-x' : 'bx bx-menu';
            toggle.setAttribute('aria-expanded', isOpen);
        });

        // User menu toggle
        const userMenuBtn = document.getElementById('userMenuBtn');
        const userMenu = document.getElementById('userMenu');
        userMenuBtn?.addEventListener('click', (e) => {
            e.stopPropagation();
            userMenu?.classList.toggle('open');
        });

        document.addEventListener('click', (e) => {
            if (!e.target.closest('#userMenu')) {
                userMenu?.classList.remove('open');
            }
        });
    }
};

// ─── "ADD TO CART" GLOBAL HANDLER ────────────────────────
function addToCart(btn) {
    const data = btn.dataset;
    Cart.add({
        id: data.id,
        name: data.name,
        price: parseFloat(data.price),
        image: data.image || '',
        size: data.size || null,
        color: data.color || null,
    });
    // Animate button
    btn.classList.add('added');
    const orig = btn.innerHTML;
    btn.innerHTML = '<i class="bx bx-check"></i> Đã thêm!';
    setTimeout(() => {
        btn.classList.remove('added');
        btn.innerHTML = orig;
    }, 1500);
}

// ─── NUMBER ANIMATION ─────────────────────────────────────
function animateNumber(el, end, duration = 1500) {
    const start = 0;
    const startTime = performance.now();
    const update = (t) => {
        const progress = Math.min((t - startTime) / duration, 1);
        const ease = 1 - Math.pow(1 - progress, 3);
        el.textContent = Math.floor(start + (end - start) * ease).toLocaleString('vi-VN');
        if (progress < 1) requestAnimationFrame(update);
    };
    requestAnimationFrame(update);
}

// ─── INTERSECTION OBSERVER (animate on scroll) ────────────
const ScrollAnim = {
    init() {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(e => {
                if (e.isIntersecting) {
                    e.target.classList.add('visible');
                    observer.unobserve(e.target);
                }
            });
        }, { threshold: 0.1, rootMargin: '0px 0px -50px 0px' });

        $$('[data-animate]').forEach(el => observer.observe(el));

        // Animate stats numbers
        $$('[data-count]').forEach(el => {
            const countObserver = new IntersectionObserver(([entry]) => {
                if (entry.isIntersecting) {
                    animateNumber(el, parseInt(el.dataset.count));
                    countObserver.unobserve(el);
                }
            });
            countObserver.observe(el);
        });
    }
};

// ─── INIT ─────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    Toast.init();
    Cart.init();
    Wishlist.init();
    Search.init();
    Navbar.init();
    ScrollAnim.init();

    // Expose globally for inline handlers
    window.Cart = Cart;
    window.Wishlist = Wishlist;
    window.addToCart = addToCart;
    window.Toast = Toast;
    window.fmt = fmt;
});
