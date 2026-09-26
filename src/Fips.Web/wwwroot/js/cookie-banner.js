(function () {
    var cookieName = 'cookie-preferences';
    var clarityId = 't9ijn4cqjo';
    var gtmId = 'GTM-KQLN72S8';

    function readPreference() {
        var match = document.cookie.match(new RegExp('(?:^|; )' + cookieName + '=([^;]*)'));
        if (!match) {
            return null;
        }

        try {
            return JSON.parse(decodeURIComponent(match[1]));
        } catch (error) {
            return null;
        }
    }

    function writePreference(analyticsOn) {
        var value = encodeURIComponent(JSON.stringify({ analytics: analyticsOn ? 'on' : 'off' }));
        var secure = window.location.protocol === 'https:' ? '; Secure' : '';
        document.cookie = cookieName + '=' + value + '; Path=/; Max-Age=31536000; SameSite=Lax' + secure;
    }

    function analyticsAccepted(preference) {
        return !!(preference && preference.analytics === 'on');
    }

    function isTrackingCookie(name) {
        return name === '_ga'
            || name.indexOf('_ga_') === 0
            || name === '_gid'
            || name.indexOf('_gat') === 0
            || name === '_gcl_au'
            || name === '_clck'
            || name === '_clsk'
            || name === 'CLID'
            || name === 'MUID'
            || name === 'ANONCHK';
    }

    function deleteCookie(name) {
        var hostname = window.location.hostname;
        var domains = ['', hostname, '.' + hostname];
        var labels = hostname.split('.');
        if (labels.length > 2) {
            var parent = labels.slice(1).join('.');
            domains.push(parent, '.' + parent);
        }

        var expires = 'Thu, 01 Jan 1970 00:00:00 GMT';
        domains.forEach(function (domain) {
            var domainPart = domain ? '; domain=' + domain : '';
            document.cookie = name + '=; expires=' + expires + '; path=/' + domainPart;
        });
    }

    function deleteTrackingCookies() {
        document.cookie.split(';').forEach(function (part) {
            var name = part.split('=')[0].trim();
            if (name && isTrackingCookie(name)) {
                deleteCookie(name);
            }
        });
    }

    function trackingNodes() {
        return document.querySelectorAll(
            'script[data-fips-tracking], script[src*="googletagmanager.com"], script[src*="google-analytics.com"], script[src*="clarity.ms"], iframe[src*="googletagmanager.com"]'
        );
    }

    function loadTracking() {
        if (!document.querySelector('script[data-fips-tracking="gtm"]')) {
            window.dataLayer = window.dataLayer || [];
            window.dataLayer.push({ 'gtm.start': new Date().getTime(), event: 'gtm.js' });
            var gtm = document.createElement('script');
            gtm.async = true;
            gtm.src = 'https://www.googletagmanager.com/gtm.js?id=' + gtmId;
            gtm.setAttribute('data-fips-tracking', 'gtm');
            document.head.appendChild(gtm);
        }

        if (!document.querySelector('script[data-fips-tracking="clarity"]')) {
            window.clarity = window.clarity || function () {
                (window.clarity.q = window.clarity.q || []).push(arguments);
            };
            var clarity = document.createElement('script');
            clarity.async = true;
            clarity.src = 'https://www.clarity.ms/tag/' + clarityId;
            clarity.setAttribute('data-fips-tracking', 'clarity');
            document.head.appendChild(clarity);
        }
    }

    function removeTracking() {
        trackingNodes().forEach(function (node) {
            node.remove();
        });

        try {
            if (typeof window.clarity === 'function') {
                window.clarity('consent', false);
            }
        } catch (error) {
            // Clarity may already have been removed.
        }

        delete window.clarity;
        delete window.google_tag_manager;
        delete window.gtag;
        window.dataLayer = undefined;
        deleteTrackingCookies();
    }

    var banner = document.querySelector('.js-cookie-banner');

    function showPanel(selector) {
        if (!banner) {
            return;
        }

        banner.querySelectorAll('.js-cookie-banner-message, .js-cookie-banner-confirmation-accept, .js-cookie-banner-confirmation-reject')
            .forEach(function (panel) {
                panel.hidden = true;
            });

        var panelToShow = banner.querySelector(selector);
        if (!panelToShow) {
            return;
        }

        panelToShow.hidden = false;
        if (panelToShow.hasAttribute('tabindex')) {
            panelToShow.focus();
        }
    }

    if (banner) {
        banner.addEventListener('click', function (event) {
            if (event.target.closest('.js-cookie-banner-accept')) {
                writePreference(true);
                loadTracking();
                showPanel('.js-cookie-banner-confirmation-accept');
                return;
            }

            if (event.target.closest('.js-cookie-banner-reject')) {
                writePreference(false);
                removeTracking();
                showPanel('.js-cookie-banner-confirmation-reject');
                return;
            }

            if (event.target.closest('.js-cookie-banner-hide')) {
                banner.hidden = true;
            }
        });
    }

    var preference = readPreference();
    if (analyticsAccepted(preference)) {
        loadTracking();
    } else {
        removeTracking();
    }

    if (!banner || document.querySelector('.cookie-preferences-form') || preference) {
        return;
    }

    banner.hidden = false;
})();
