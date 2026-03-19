const axios = require('axios');
const cfg = require('./config');

const api = axios.create({
  baseURL: `https://api.github.com/repos/${cfg.github.repo}/contents`,
  headers: {
    Authorization: `token ${cfg.github.token}`,
    Accept: 'application/vnd.github.v3+json',
  }
});

async function getFile(path) {
  try {
    const res = await api.get(`/${path}`);
    const content = Buffer.from(res.data.content.replace(/\n/g, ''), 'base64').toString('utf8');
    return { content, sha: res.data.sha, exists: true };
  } catch (e) {
    if (e.response?.status === 404) return { content: null, sha: null, exists: false };
    throw e;
  }
}

async function putFile(path, content, sha, message) {
  const encoded = Buffer.from(content, 'utf8').toString('base64');
  const body = { message, content: encoded, branch: cfg.github.branch };
  if (sha) body.sha = sha;
  const res = await api.put(`/${path}`, body);
  return {
    commitSha: res.data.commit?.sha?.slice(0, 7),
    commitUrl: `https://github.com/${cfg.github.repo}/commit/${res.data.commit?.sha}`,
  };
}

module.exports = { getFile, putFile };
