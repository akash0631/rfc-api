// v2-rfc CLI — Central Config
// All credentials and constants in one place

module.exports = {
  github: {
    token: process.env.GITHUB_TOKEN || ''  // set via: export GITHUB_TOKEN=ghp_...,
    repo: 'akash0631/rfc-api',
    branch: 'master',
    files: {
      dabConfig: 'dab-config.json',
      swagger: 'v2_sap_api_explorer.html',
      portalIndex: 'portal/index.html',
    }
  },
  azure: {
    dabAppUrl: 'https://my-dab-app.azurewebsites.net',
    dabOpenApiUrl: 'https://my-dab-app.azurewebsites.net/api/openapi',
    iisServer: 'V2DC-ADDVERB',
    iisUser: 'Administrator',
    iisPass: 'vrl@55555',
    msdeployUrl: 'https://V2DC-ADDVERB:8172/msdeploy.axd',
  },
  cloudflare: {
    accountId: '33b6cfffad5dd935e73e9061a56f1506',
    pagesProject: 'v2-rfc-portal',
    portalUrl: 'https://sap-api.v2retail.net',
    swaggerUrl: 'https://sap-api.v2retail.net/swagger',
  },
  anthropic: {
    model: 'claude-sonnet-4-20250514',
    maxTokens: 2500,
  },
  sap: {
    environments: {
      dev:        { fn: 'rfcConfigparameters',           host: '192.168.144.174', client: '210',  sysId: 'DEV' },
      quality:    { fn: 'rfcConfigparametersquality',    host: '192.168.144.179', client: '600',  sysId: 'S4Q' },
      production: { fn: 'rfcConfigparametersproduction', host: '192.168.144.170', client: '600',  sysId: 'PRD' },
    },
    defaultEnv: 'dev',
    user: 'POWERBI',
    password: 'India@123456',
  },
  folderMap: {
    Finance:           'Controllers/Finance',
    GateEntry:         'Controllers/GateEntry_LOT_Putway',
    Vendor:            'Controllers/Vendor_SRM_Routing',
    HUCreation:        'Controllers/HU_Creation',
    FabricPutway:      'Controllers/FMS_FABRIC_PUTWAY',
    HRMS:              'Controllers/HRMS',
    NSO:               'Controllers/NSO',
    PaperlessPicklist: 'Controllers/PaperlessPicklist',
    Sampling:          'Controllers/Sampling',
    VehicleLoading:    'Controllers/Vehicle_Loading',
  }
};
