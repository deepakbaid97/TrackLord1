/**
 * Central Configuration Schema & Registry for Tenant Settings
 * Single Source of Truth: Adding a new configurable feature here automatically
 * renders it in the Tenant Settings UI and enables declarative FeatureGate checks.
 */

export const CLIENT_MODES = [
  {
    id: 'lot',
    name: 'Per Production Lot',
    icon: 'reorder',
    tag: 'Single Client',
    description: 'A production lot belongs to a single customer for its entire lifecycle. Client is selected when creating a lot, and all WIP is attributed directly to that client.'
  },
  {
    id: 'step',
    name: 'Per Step Movement (Job-Work)',
    icon: 'alt_route',
    tag: 'Subcontractors',
    description: 'Clients or subcontractors are assigned dynamically at each manufacturing step transfer (e.g. cutting in-house, stitching subcontracted, washing sent to external vendor).'
  },
  {
    id: 'none',
    name: 'Disabled / Internal Only',
    icon: 'block',
    tag: 'No Clients',
    description: 'Client and customer tracking is completely hidden. Ideal for internal standard-run garment manufacturing without client attribution.'
  }
];

export const CONFIG_SCHEMA = [
  {
    categoryId: 'pages',
    categoryName: 'Pages & Navigation',
    description: 'Control navigation visibility and route access for your organization',
    icon: 'web',
    items: [
      {
        key: 'pages.dashboard',
        label: 'Dashboard',
        description: 'Overview metrics, factory WIP, and step summaries',
        default: true
      },
      {
        key: 'pages.inventory',
        label: 'Inventory (Material Lots)',
        description: 'Raw material stock, intake, and material lot ledger',
        default: true
      },
      {
        key: 'pages.lots',
        label: 'Production Lots',
        description: 'Active production batches, step progress, and lot journeys',
        default: true
      },
      {
        key: 'pages.steps',
        label: 'Manufacturing Steps',
        description: 'Process steps, routing order, and terminal step configurations',
        default: true
      },
      {
        key: 'pages.materials',
        label: 'Material Definitions',
        description: 'Master raw materials catalog and standard unit definitions',
        default: true
      },
      {
        key: 'pages.clients',
        label: 'Clients & Customers',
        description: 'Client directory, customer codes, and contact details',
        default: true
      },
      {
        key: 'pages.users',
        label: 'User Management',
        description: 'Manage tenant operators, supervisors, and role assignments',
        default: true
      }
    ]
  },
  {
    categoryId: 'dashboard',
    categoryName: 'Dashboard Widgets',
    description: 'Toggle visibility of individual KPI metric cards and analytics sections on the dashboard',
    icon: 'dashboard',
    items: [
      {
        key: 'dashboard.kpi.activeLots',
        label: 'Active Lots KPI Card',
        description: 'Display active production lots counter card on dashboard',
        default: true
      },
      {
        key: 'dashboard.kpi.totalWip',
        label: 'Total WIP KPI Card',
        description: 'Display total in-progress pieces counter card on dashboard',
        default: true
      },
      {
        key: 'dashboard.sections.wipByStep',
        label: 'WIP by Step Breakdown',
        description: 'Display manufacturing step visual progress bars and quantity breakdown',
        default: true
      },
      {
        key: 'dashboard.sections.clientDistribution',
        label: 'Client Distribution Table',
        description: 'Display customer volume breakdown and moved quantities table',
        default: true
      },
      {
        key: 'dashboard.sections.materialAvailability',
        label: 'Material Availability Section',
        description: 'Display raw material inventory stock table and total units available',
        default: true
      },
      {
        key: 'dashboard.sections.activeLots',
        label: 'Active Production Lots Table',
        description: 'Display live active lot progress table with primary step badges',
        default: true
      }
    ]
  },
  {
    categoryId: 'forms',
    categoryName: 'Forms & Modals',
    description: 'Show or hide creation, import, and scrap recording actions',
    icon: 'dynamic_form',
    items: [
      {
        key: 'forms.lots.create',
        label: 'Create Production Lot Modal',
        description: 'Allow creating new production lots manually in the UI',
        default: true
      },
      {
        key: 'forms.lots.import',
        label: 'Bulk Import Production Lots',
        description: 'Allow bulk uploading lots via Excel/CSV spreadsheets',
        default: true
      },
      {
        key: 'forms.lots.reject',
        label: 'Reject & Scrap Items Action',
        description: 'Allow recording rejections and scrap quantities at steps',
        default: true
      },
      {
        key: 'forms.lots.delete',
        label: 'Delete Production Lot Action',
        description: 'Allow deleting lots and reversing their movements/consumptions',
        default: true
      },
      {
        key: 'forms.inventory.create',
        label: 'Add Raw Material Batch Modal',
        description: 'Allow manual intake of raw material rolls/batches',
        default: true
      },
      {
        key: 'forms.inventory.import',
        label: 'Bulk Import Raw Materials',
        description: 'Allow uploading raw material stock spreadsheets',
        default: true
      }
    ]
  },
  {
    categoryId: 'fields',
    categoryName: 'Fields & UI Sections',
    description: 'Toggle specific data columns, financial rates, and visual timeline tabs',
    icon: 'view_column',
    items: [
      {
        key: 'fields.lots.client',
        label: 'Client Column / Tag in Lots',
        description: 'Display associated customer/client badge in production lot lists',
        default: true
      },
      {
        key: 'fields.lots.timeline',
        label: 'Movement Journey Timeline',
        description: 'Show visual step progression timeline in lot detail views',
        default: true
      },
      {
        key: 'fields.lots.materialTraceability',
        label: 'Material Traceability Tab',
        description: 'Show BOM consumed materials and roll batch traceability',
        default: true
      },
      {
        key: 'fields.inventory.cost',
        label: 'Material Unit Rates / Pricing',
        description: 'Show unit cost and financial rates on raw material inventory',
        default: true
      },
      {
        key: 'fields.lots.remarks',
        label: 'Remarks & Notes Fields',
        description: 'Display custom operational notes and remarks across forms',
        default: true
      }
    ]
  }
];
