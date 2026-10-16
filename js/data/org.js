/**
 * The company: 8 departments and every role in them (CEO system, #/ceo).
 * Each role has a title, a department, who holds it (an AI agent or a human), its duties, the admin console sections
 * it may open, the actions it may take, and its money limit. AI agents sign in to the admin console as their role;
 * with the backend each one is a real agent with these exact permissions.
 *
 * sections: admin console NAV keys ('*' = all) · can: actions (see ACTIONS) · limit: the most money the role may
 * move alone (EGP); more than that goes to the CEO · watch: audit-log actions that belong to the role's area.
 */
(function (SA) {
  const DEPTS = [
    { key: 'ops', name: 'Operations', head: 'coo', icon: 'grid', tone: 'turf', text: 'Bookings, orders, the factory and delivery run on time.' },
    { key: 'support', name: 'Customer support & trust', head: 'support-lead', parent: 'ops', icon: 'message', tone: 'info', text: 'Every customer, partner and seller gets a fair, fast answer; reviews and communities stay clean.' },
    { key: 'bizdev', name: 'Business development', head: 'partners-agent', parent: 'ops', icon: 'pitch', tone: 'gold', text: 'New pitch partners, sellers and factories join and grow.' },
    { key: 'finance', name: 'Finance', head: 'cfo', icon: 'wallet', tone: 'blaze', text: 'Money in, money out, every pound reconciled.' },
    { key: 'legal', name: 'Legal & compliance', head: 'counsel', icon: 'shield', tone: 'ink', text: 'Contracts, taxes, data protection and ID checks.' },
    { key: 'marketing', name: 'Marketing & social', head: 'cmo', icon: 'megaphone', tone: 'violet', text: 'Growth, campaigns, social media, content and fantasy.' },
    { key: 'tech', name: 'Technology & product', head: 'cto', icon: 'settings', tone: 'info', text: 'The platform works, is fast and safe.' },
    { key: 'aiops', name: 'AI agents operations', head: 'conductor', icon: 'bolt', tone: 'gold', text: 'Every AI agent works within its permissions, on budget, without errors.' },
  ];

  /** Actions a role may take (the permissions matrix) */
  const ACTIONS = [
    ['payout.approve', 'Send withdrawals', 'Money'], ['payout.prepare', 'Prepare withdrawals', 'Money'], ['settle', 'Pay the factory', 'Money'],
    ['refund', 'Refund customers', 'Money'], ['credit', 'Credit customers', 'Money'], ['adjust', 'Adjust balances', 'Money'],
    ['approve.partner', 'Approve pitch partners', 'People'], ['approve.seller', 'Approve sellers', 'People'], ['suspend', 'Suspend accounts', 'People'], ['customer.block', 'Block customers', 'People'],
    ['moderate', 'Moderate reviews & posts', 'Trust'], ['designs', 'Review designs', 'Trust'],
    ['promo', 'Create promo codes', 'Marketing'], ['broadcast', 'Send broadcasts', 'Marketing'], ['content', 'Edit site content', 'Marketing'], ['fantasy', 'Run fantasy', 'Marketing'],
    ['factory', 'Manage factories & courier', 'Operations'], ['orders', 'Change orders & bookings', 'Operations'],
    ['settings', 'Platform settings', 'Platform'], ['audit', 'Read the audit log', 'Platform'],
  ];

  const ALL = ACTIONS.map(([k]) => k);
  const ROLES = [
    { id: 'ceo', title: 'Chief executive (CEO)', dept: null, holder: 'human', name: 'Sharaf', sections: ['*'], can: ALL, limit: Infinity,
      duties: ['Sets goals and budgets', 'Approves big payments and refunds', 'Hires, pauses and stops AI agents', 'Owns every kill switch'] },
    { id: 'hq', title: 'Admin manager (HQ)', dept: 'ops', reportsTo: 'ceo', holder: 'human', name: 'Super App HQ', account: 'acc-admin', sections: ['*'], can: ALL.filter((k) => k !== 'payout.prepare'), limit: 10000,
      duties: ['Runs the admin console day to day', 'Steps in when an agent is unsure', 'Second pair of eyes on agents’ work'] },
    { id: 'cfo', title: 'Finance director (CFO)', dept: 'finance', reportsTo: 'ceo', holder: 'human', name: 'Laila Mansour', account: 'acc-cfo', pin: '7777', sections: ['overview', 'payouts', 'finance-approvals', 'revenue', 'customers', 'partners', 'sellers', 'analytics', 'audit'], can: ['payout.approve', 'settle', 'refund', 'credit', 'adjust', 'audit'], limit: 50000,
      duties: ['Approves withdrawals the payables agent prepared', 'Second approval on anything over the two-person limit', 'Monthly close and reconciliation', 'Taxes with the legal counsel'], watch: ['admin.payout', 'admin.printer', 'admin.balance'] },

    { id: 'coo', title: 'Operations lead', dept: 'ops', reportsTo: 'ceo', holder: 'ai', name: 'Atlas', cost: 2400, sections: ['overview', 'orders', 'approvals', 'factories', 'support', 'analytics'], can: ['orders', 'factory', 'audit'], limit: 0,
      duties: ['Watches bookings, orders and the factory every hour', 'Escalates late jobs and problems', 'Writes the daily operations summary'], watch: ['admin.booking', 'admin.order', 'factory.'] },
    { id: 'bookings-ops', title: 'Pitch & bookings agent', dept: 'ops', reportsTo: 'coo', holder: 'ai', name: 'Kick', cost: 1500, sections: ['overview', 'orders', 'tournaments', 'games'], can: ['orders'], limit: 0,
      duties: ['Checks double bookings and no-shows', 'Weekly packages and make-up games', 'Tournaments and free-slot games'], watch: ['admin.booking', 'admin.package', 'admin.games', 'admin.slot', 'booking.', 'checkin.'] },
    { id: 'orders-ops', title: 'Orders & factory agent', dept: 'ops', reportsTo: 'coo', holder: 'ai', name: 'Loom', cost: 1500, sections: ['overview', 'orders', 'store', 'factories', 'samples'], can: ['orders', 'factory'], limit: 0,
      duties: ['Sends printed orders to the factory', 'Chases late jobs and quality problems', 'Keeps the Super App Store stocked'], watch: ['admin.factory', 'admin.sample', 'admin.store', 'factory.job', 'pod.'] },
    { id: 'logistics', title: 'Courier & delivery agent', dept: 'ops', reportsTo: 'coo', holder: 'ai', name: 'Route', cost: 1100, sections: ['overview', 'factories', 'orders'], can: ['factory'], limit: 0,
      duties: ['Books the courier when an order is packed', 'Follows every parcel to delivery', 'Handles refused parcels'], watch: ['factory.shipment', 'admin.shipping'] },

    { id: 'support-lead', title: 'Support & trust lead', dept: 'support', reportsTo: 'coo', holder: 'ai', name: 'Haven', cost: 2000, sections: ['overview', 'support', 'customers', 'reviews', 'orders'], can: ['refund', 'credit', 'customer.block', 'moderate'], limit: 1000,
      duties: ['Answers disputes and decides refunds up to 1,000 EGP', 'Blocks abusive customers', 'Reviews the frontline agent’s answers'], watch: ['admin.support', 'admin.customer', 'support.'] },
    { id: 'support-agent', title: 'Frontline support agent', dept: 'support', reportsTo: 'support-lead', holder: 'ai', name: 'Echo', cost: 1800, sections: ['support', 'customers', 'orders'], can: ['refund'], limit: 300,
      duties: ['Replies to every ticket within 2 hours', 'Refunds up to 300 EGP', 'Hands harder cases to the lead'], watch: ['admin.support', 'support.'] },
    { id: 'trust', title: 'Trust & safety agent', dept: 'support', reportsTo: 'support-lead', holder: 'ai', name: 'Shield', cost: 1300, sections: ['reviews', 'pod-review', 'community', 'sellers'], can: ['moderate', 'designs', 'suspend'], limit: 0,
      duties: ['Checks designs for copyright', 'Moderates reviews and communities', 'Gives sellers strikes'], watch: ['admin.design', 'admin.review', 'admin.community', 'admin.seller.strike', 'design.'] },

    { id: 'partners-agent', title: 'Partnerships agent', dept: 'bizdev', reportsTo: 'coo', holder: 'ai', name: 'Bridge', cost: 1400, sections: ['partners', 'approvals'], can: ['approve.partner'], limit: 0,
      duties: ['Reviews pitch partner applications', 'Onboards new pitches', 'Talks commission deals (the CEO signs)'], watch: ['admin.partner', 'admin.pitch', 'partner.'] },
    { id: 'sellers-agent', title: 'Sellers agent', dept: 'bizdev', reportsTo: 'partners-agent', holder: 'ai', name: 'Spark', cost: 1400, sections: ['sellers', 'approvals', 'levels', 'samples', 'pod-catalog'], can: ['approve.seller'], limit: 0,
      duties: ['Checks seller IDs and approves sellers', 'Runs the monthly level review', 'Helps new sellers make their first sale'], watch: ['admin.seller', 'admin.level', 'seller.'] },

    { id: 'payables', title: 'Payables agent', dept: 'finance', reportsTo: 'cfo', holder: 'ai', name: 'Ledger', cost: 1600, sections: ['payouts', 'partners', 'sellers'], can: ['payout.prepare'], limit: 0,
      duties: ['Checks every withdrawal (ID, disputes, balance)', 'Prepares withdrawals — never sends them', 'Prepares the factory settlement'], watch: ['admin.payout', 'admin.printer'] },
    { id: 'analyst', title: 'Revenue analyst', dept: 'finance', reportsTo: 'cfo', holder: 'ai', name: 'Abacus', cost: 1200, sections: ['revenue', 'analytics'], can: [], limit: 0,
      duties: ['Daily revenue and cost report', 'Reconciles payments with the bank', 'Flags unusual numbers'], watch: ['admin.revenue', 'payment.'] },

    { id: 'counsel', title: 'Legal counsel', dept: 'legal', reportsTo: 'ceo', holder: 'human', name: 'Fictional law firm (external)', sections: [], can: [], limit: 0,
      duties: ['Contracts with partners, the factory and the courier', 'Tax filings with the CFO', 'Data protection (Law 151 of 2020)'] },
    { id: 'compliance', title: 'Compliance agent', dept: 'legal', reportsTo: 'counsel', holder: 'ai', name: 'Clause', cost: 1000, sections: ['audit', 'sellers', 'partners', 'approvals'], can: ['audit'], limit: 0,
      duties: ['Keeps the compliance calendar', 'Checks seller and partner IDs', 'Reads the audit log for rule breaks'], watch: ['admin.impersonate', 'admin.settings', 'ceo.'] },

    { id: 'cmo', title: 'Marketing lead', dept: 'marketing', reportsTo: 'ceo', holder: 'ai', name: 'Pulse', cost: 2200, sections: ['overview', 'promos', 'broadcast', 'analytics', 'content'], can: ['promo', 'broadcast', 'content'], limit: 0,
      duties: ['Plans campaigns within the budget', 'Creates promo codes', 'Sends broadcasts to the right people'], watch: ['admin.promo', 'admin.broadcast'] },
    { id: 'social', title: 'Social media & content agent', dept: 'marketing', reportsTo: 'cmo', holder: 'ai', name: 'Buzz', cost: 1700, sections: ['content', 'community', 'broadcast'], can: ['content'], limit: 0,
      duties: ['Posts on social media every day', 'News, banners and the announcement bar', 'Runs the official communities'], watch: ['admin.content', 'admin.news', 'admin.banner', 'admin.community.post'] },
    { id: 'arena', title: 'Fantasy & games agent', dept: 'marketing', reportsTo: 'cmo', holder: 'ai', name: 'Arena', cost: 900, sections: ['fantasy', 'games', 'community'], can: ['fantasy'], limit: 0,
      duties: ['Runs fantasy leagues and prizes', 'Free-slot games and player safety', 'Match-day content'], watch: ['admin.fantasy', 'admin.games', 'fantasy.'] },

    { id: 'cto', title: 'Technology lead', dept: 'tech', reportsTo: 'ceo', holder: 'ai', name: 'Forge', cost: 2600, sections: ['settings', 'content', 'analytics', 'audit'], can: ['settings', 'audit'], limit: 0,
      duties: ['Keeps the platform up and fast', 'Releases new features', 'Security and backups'], watch: ['admin.settings', 'platform.'] },
    { id: 'qa', title: 'Quality & monitoring agent', dept: 'tech', reportsTo: 'cto', holder: 'ai', name: 'Probe', cost: 900, sections: ['overview', 'analytics'], can: [], limit: 0,
      duties: ['Tests every flow every night', 'Watches errors and speed', 'Reports bugs to the technology lead'], watch: [] },

    { id: 'conductor', title: 'AI agents supervisor', dept: 'aiops', reportsTo: 'ceo', holder: 'ai', name: 'Conductor', cost: 1300, sections: ['overview', 'audit'], can: ['audit'], limit: 0,
      duties: ['Watches every agent’s work and cost', 'Pauses an agent that makes mistakes', 'Weekly agents report to the CEO'], watch: ['ceo.agent'] },
  ];

  SA.data.org = { DEPTS, ROLES, ACTIONS };
})(window.SA);
