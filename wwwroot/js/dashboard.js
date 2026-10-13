(() => {
    const groups = [...document.querySelectorAll('[data-motion]')];
    if (!groups.length) return;

    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    const replay = document.querySelector('.replay-motion');
    const activeAnimations = new Set();
    const counters = new Map();
    const formats = new Map();
    let observer;

    function format(value, decimals = 0) {
        if (!formats.has(decimals)) {
            formats.set(decimals, new Intl.NumberFormat('zh-TW', {
                minimumFractionDigits: decimals, maximumFractionDigits: decimals
            }));
        }
        return formats.get(decimals).format(value);
    }

    // All final values and chart shapes are server-rendered. Motion only enhances them.
    function animate(element, frames, options) {
        if (reducedMotion.matches || !element.animate) return;
        const animation = element.animate(frames, { fill: 'backwards', ...options });
        activeAnimations.add(animation);
        const clear = () => { activeAnimations.delete(animation); animation.cancel(); };
        animation.finished.then(clear, () => activeAnimations.delete(animation));
    }

    function finishCounter(element) {
        const pending = counters.get(element);
        if (pending) cancelAnimationFrame(pending);
        counters.delete(element);
        element.classList.remove('is-counting');
    }

    function countUp(element, duration = 1400, delay = 0) {
        finishCounter(element);
        if (reducedMotion.matches) return;
        const target = Number(element.dataset.value);
        const decimals = Number(element.dataset.decimals);
        if (!Number.isFinite(target) || target === 0) return;
        const display = element.querySelector('.count-current');
        const start = performance.now() + delay;
        display.textContent = format(0, decimals);
        element.classList.add('is-counting');
        function step(now) {
            const progress = Math.max(0, Math.min(1, (now - start) / duration));
            // Decelerate into the exact total, without overshooting financial values.
            display.textContent = format(target * (1 - Math.pow(1 - progress, 4)), decimals);
            if (progress < 1) counters.set(element, requestAnimationFrame(step));
            else finishCounter(element);
        }
        counters.set(element, requestAnimationFrame(step));
    }

    function play(group) {
        group.dataset.motionPlayed = 'true';
        if (reducedMotion.matches) return;
        if (group.dataset.motion === 'stats') {
            group.querySelectorAll('.stat-card').forEach((card, index) => {
                animate(card, [{ opacity: 0, transform: 'translateY(16px)' },
                    { opacity: 1, transform: 'translateY(0)' }],
                { duration: 720, delay: index * 95, easing: 'cubic-bezier(.16,1,.3,1)' });
                card.querySelectorAll('.count-up').forEach(number => countUp(number, 1400, index * 95));
            });
        } else if (group.dataset.motion === 'trend') {
            group.querySelectorAll('.chart-group').forEach((column, index) => {
                column.querySelectorAll('.bar').forEach((bar, series) => {
                    animate(bar, [{ transform: 'scaleY(0)', opacity: .35 },
                        { transform: 'scaleY(1)', opacity: 1 }],
                    { duration: 1150, delay: 100 + index * 100 + series * 70,
                        easing: 'cubic-bezier(.22,1,.36,1)' });
                });
            });
        } else if (group.dataset.motion === 'occupancy') {
            const ring = group.querySelector('.ring-progress');
            animate(ring, [{ strokeDashoffset: '100' },
                { strokeDashoffset: ring.getAttribute('stroke-dashoffset') }],
            { duration: 1700, delay: 160, easing: 'cubic-bezier(.22,1,.36,1)' });
            group.querySelectorAll('.count-up').forEach(number => countUp(number, 1700, 160));
        }
    }

    function settle() {
        for (const animation of activeAnimations) animation.cancel();
        activeAnimations.clear();
        for (const element of [...counters.keys()]) finishCounter(element);
    }

    function revealOrObserve(group) {
        const bounds = group.getBoundingClientRect();
        if (bounds.top < innerHeight - 20 && bounds.bottom > 0) play(group);
        else observer?.observe(group);
    }

    if ('IntersectionObserver' in window) {
        observer = new IntersectionObserver(entries => {
            for (const entry of entries) {
                if (entry.isIntersecting && entry.target.dataset.motionPlayed !== 'true') {
                    play(entry.target);
                    observer.unobserve(entry.target);
                }
            }
        }, { threshold: .15 });
        groups.forEach(group => observer.observe(group));
    } else groups.forEach(play);

    replay.hidden = reducedMotion.matches || !Element.prototype.animate;
    replay.addEventListener('click', () => {
        settle();
        groups.forEach(group => {
            group.dataset.motionPlayed = 'false';
            revealOrObserve(group);
        });
        animate(replay.querySelector('span'), [{ transform: 'rotate(0)' },
            { transform: 'rotate(360deg)' }], { duration: 650, easing: 'ease-out' });
    });
    reducedMotion.addEventListener('change', () => {
        settle();
        replay.hidden = reducedMotion.matches || !Element.prototype.animate;
    });
    window.addEventListener('pagehide', () => { settle(); observer?.disconnect(); });
    window.addEventListener('pageshow', event => {
        if (event.persisted) groups.filter(group => group.dataset.motionPlayed !== 'true')
            .forEach(group => observer?.observe(group));
    });

    // One stable detail area serves hover, keyboard focus, and touch without covering data.
    const detail = document.querySelector('.trend-values');
    const invitation = document.querySelector('.trend-invitation');
    const columns = [...document.querySelectorAll('.chart-group')];
    function selectColumn(column) {
        columns.forEach(item => item.classList.toggle('is-selected', item === column));
        invitation.hidden = true;
        detail.hidden = false;
        detail.querySelector('.detail-month').textContent = column.dataset.month;
        detail.querySelector('.detail-expected').textContent = 'NT$ ' + format(Number(column.dataset.expected));
        detail.querySelector('.detail-received').textContent = 'NT$ ' + format(Number(column.dataset.received));
    }
    columns.forEach((column, index) => {
        column.addEventListener('pointerenter', event => {
            if (event.pointerType !== 'touch') selectColumn(column);
        });
        column.addEventListener('focus', () => selectColumn(column));
        column.addEventListener('click', () => selectColumn(column));
        column.addEventListener('keydown', event => {
            let next;
            if (event.key === 'ArrowRight') next = (index + 1) % columns.length;
            if (event.key === 'ArrowLeft') next = (index - 1 + columns.length) % columns.length;
            if (event.key === 'Home') next = 0;
            if (event.key === 'End') next = columns.length - 1;
            if (next !== undefined) { event.preventDefault(); columns[next].focus(); }
        });
    });
})();
