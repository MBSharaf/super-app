/**
 * About us (#/about) and Contact us (#/contact) — public pages (owner, 2026-11-23, for the Paymob review).
 * About lists only the services open on the demo site (no store, fantasy or match schedule while they are closed).
 * The phone and email come from SA.config.contact (js/core/namespace.js).
 */
(function (SA) {
  const { esc } = SA.core.utils;
  const { icon } = SA.ui;
  const C = () => SA.config.contact;
  const telHref = () => `tel:${C().phone.replace(/[^\d+]/g, '')}`;
  const mailHref = () => `mailto:${C().email}`;

  const SERVICES = [
    ['pitch', 'Book football pitches', 'Find 5, 7 and 11-a-side pitches near you, see the free hours live and book in a few taps. Pay a deposit online and the rest at the pitch, with a QR ticket for check-in.', '#/pitches'],
    ['padel', 'Book padel courts', 'Book a padel court for 60, 90 or 120 minutes, rent rackets and balls, or join an Open Match at your level.', '#/pitches?sport=padel'],
    ['users', 'Find players', 'Short on players? Post your game and let players nearby join, or join a game that needs one more.', '#/players'],
    ['bell', 'Free-slot alerts', 'Follow your favourite pitches and areas and get told the moment an hour frees up.', '#/alerts'],
    ['trophy', 'Tournaments', 'Cups and leagues at pitches near you. Registration opens soon.', '#/tournaments'],
    ['message', 'Communities', 'Talk football with players in your area and hear first about offers and free hours.', '#/community'],
    ['wallet', 'Wallet & points', 'One wallet for every booking. Earn a point for every pound you spend and use your points on your next game.', '#/wallet'],
    ['dashboard', 'For pitch owners', 'A partner dashboard to list pitches and padel courts, manage bookings, post free hours, check players in and see the money.', '#/partner/apply'],
  ];

  const contactCards = () => `<div class="ab-contact">
      <a class="ab-contact-card" href="${telHref()}"><span class="ab-ico">${icon('message', 20)}</span><span class="grow"><span class="xs muted">Phone</span><b dir="ltr" data-i18n-skip>${esc(C().phoneLabel)}</b></span></a>
      <a class="ab-contact-card" href="${mailHref()}"><span class="ab-ico">${icon('send', 20)}</span><span class="grow"><span class="xs muted">Email</span><b dir="ltr" data-i18n-skip>${esc(C().email)}</b></span></a>
    </div>`;

  SA.pages.about = {
    title: 'About us — Super Malaab',
    render() {
      return `<div class="container section-tight ab-page">
        <header class="page-head"><span class="eyebrow">About us</span><h1 class="h1" style="margin-top:8px">Super Malaab</h1>
          <p class="lead">Super Malaab is an Egyptian sports app that brings players, pitches and padel courts together in one place.</p></header>
        <section class="panel panel-pad ab-story">
          <h2 class="h3">Who we are</h2>
          <p>We are a team from Cairo who love football. Booking a pitch in Egypt usually means phone calls, waiting and guessing which hours are free. We built Super Malaab so you can see the free hours, book and pay in a minute, and fill your squad without chasing anyone.</p>
          <h2 class="h3">What we do</h2>
          <p>We work with pitch and padel court owners across Egypt. Players book and pay through Super Malaab, and owners manage their pitches, bookings and money from one dashboard. Payments are protected, every payment has a receipt, and our support team is here when something goes wrong.</p>
        </section>
        <h2 class="h2 ab-sub">Our services</h2>
        <div class="ab-services">${SERVICES.map(([ic, t, d, href]) => `<a class="ab-service" href="${href}"><span class="ab-ico">${icon(ic, 22)}</span><b>${t}</b><span class="small soft">${d}</span></a>`).join('')}</div>
        <section class="panel panel-pad ab-reach"><h2 class="h3">Get in touch</h2><p class="small soft">Questions, partnerships or a problem with a booking — we’re happy to help.</p>${contactCards()}
          <a class="btn btn-outline btn-sm" href="#/contact" style="margin-top:14px">Contact us ${icon('arrowRight', 14)}</a></section>
      </div>`;
    },
  };

  SA.pages.contact = {
    title: 'Contact us — Super Malaab',
    render() {
      return `<div class="container section-tight ab-page">
        <header class="page-head"><span class="eyebrow">Contact us</span><h1 class="h1" style="margin-top:8px">We’re here to help</h1>
          <p class="lead">Call us, email us or open a support request. We usually reply within a few hours.</p></header>
        ${contactCards()}
        <section class="panel panel-pad ab-reach">
          <h2 class="h3">Booking or payment problem?</h2>
          <p class="small soft">Open the booking and tap “Get help”, or send a support request so the team sees all the details.</p>
          <div class="row wrap" style="--gap:10px;margin-top:14px"><a class="btn btn-primary btn-sm" href="#/support">${icon('message', 15)} Open a support request</a><a class="btn btn-outline btn-sm" href="#/help">${icon('help', 15)} Help center</a></div>
        </section>
        <section class="panel panel-pad ab-reach">
          <h2 class="h3">Pitch or padel court owner?</h2>
          <p class="small soft">List your pitches on Super Malaab and get bookings online.</p>
          <a class="btn btn-outline btn-sm" href="#/apply" style="margin-top:14px">Become a partner ${icon('arrowRight', 14)}</a>
        </section>
        <p class="small muted" style="margin-top:18px">Super Malaab · Cairo, Egypt · <a class="link" href="#/about">About us</a> · <a class="link" href="#/legal">Terms & policies</a></p>
      </div>`;
    },
  };
})(window.SA);
