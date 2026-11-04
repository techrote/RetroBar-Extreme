'use strict';

// Execute the production inline GitHub script with API fixtures; no GitHub writes.
const {readFileSync} = require('node:fs');
const {resolve} = require('node:path');
const assert = require('node:assert/strict');
const test = require('node:test');
const workflow = readFileSync(resolve(__dirname, '../../.github/workflows/comment-pr-artifacts.yml'), 'utf8').replace(/\r\n/g, '\n');
const script = workflow.split('          script: |\n')[1].split('\n')
  .map(line => line.replace(/^            /, '')).join('\n');
const execute = new (Object.getPrototypeOf(async function () {}).constructor)('github', 'context', 'core', script);
const sha = 'a'.repeat(40);
const marker = '<!-- bot: nightly-link -->';
const bot = {login: 'github-actions[bot]', type: 'Bot'};
const currentPR = (overrides = {}) => ({
  number: 35, state: 'open', head: {sha, ref: 'audit', repo: {id: 1}},
  base: {repo: {full_name: 'owner/retrobar'}}, ...overrides,
});

async function runFixture({run = {}, pulls = [currentPR()], reads, comments = [], artifacts = [{id: 101, name: 'Portable', expired: false}]} = {}) {
  const writes = [];
  const observed = [];
  let readIndex = 0;
  const rest = {
    pulls: {list() {}, async get(args) {
      const pr = reads ? reads[Math.min(readIndex++, reads.length - 1)] : pulls.find(p => p.number === args.pull_number);
      assert.ok(pr, 'unexpected PR read');
      return {data: pr};
    }},
    actions: {listWorkflowRunArtifacts() {}},
    issues: {
      listComments() {},
      async createComment(args) { writes.push({type: 'create', ...args}); },
      async updateComment(args) { writes.push({type: 'update', ...args}); },
    },
  };
  const github = {rest, async paginate(endpoint, args) {
    observed.push(args);
    if (endpoint === rest.pulls.list) return pulls;
    if (endpoint === rest.actions.listWorkflowRunArtifacts) return artifacts;
    if (endpoint === rest.issues.listComments) return comments;
    throw new Error('Unexpected API endpoint');
  }};
  const context = {repo: {owner: 'owner', repo: 'retrobar'}, payload: {workflow_run: {
    id: 100, run_attempt: 2, event: 'pull_request', conclusion: 'success',
    repository: {full_name: 'owner/retrobar'}, head_repository: {id: 1},
    head_sha: sha, head_branch: 'audit', pull_requests: [{number: 35}], ...run,
  }}};
  await execute(github, context, {info() {}, warning() {}});
  return {writes, observed};
}

test('current source publishes SHA and run/attempt; expired downloads are omitted', async () => {
  const {writes} = await runFixture({artifacts: [
    {id: 101, name: 'Portable', expired: false}, {id: 102, name: 'Expired', expired: true},
  ]});
  assert.equal(writes.length, 1);
  assert.equal(writes[0].type, 'create');
  assert.ok(writes[0].body.includes(sha));
  assert.match(writes[0].body, /runs\/100\/attempts\/2/);
  assert.match(writes[0].body, /pull-request merge builds/);
  assert.match(writes[0].body, /artifacts\/101\.zip/);
  assert.doesNotMatch(writes[0].body, /Expired|artifacts\/102/);
});

test('an old workflow snapshot cannot overwrite downloads after a push or closure', async () => {
  for (const pr of [currentPR({head: {sha: 'b'.repeat(40), ref: 'audit', repo: {id: 1}}}), currentPR({state: 'closed'})]) {
    const {writes} = await runFixture({pulls: [pr], comments: [{id: 10, user: bot, body: marker + '\nCurrent downloads'}]});
    assert.deepEqual(writes, []);
  }
});

test('a push during comment reads is detected immediately before publication', async () => {
  const changed = currentPR({head: {sha: 'b'.repeat(40), ref: 'audit', repo: {id: 1}}});
  assert.deepEqual((await runFixture({reads: [currentPR(), changed]})).writes, []);
});

test('fork fallback requires matching head repository and branch as well as commit', async () => {
  const valid = currentPR({head: {sha, ref: 'audit', repo: {id: 2}}});
  const wrongBranch = currentPR({number: 36, head: {sha, ref: 'different', repo: {id: 2}}});
  const wrongRepository = currentPR({number: 37, head: {sha, ref: 'audit', repo: {id: 3}}});
  const {writes} = await runFixture({run: {head_repository: {id: 2}, pull_requests: []}, pulls: [wrongRepository, wrongBranch, valid]});
  assert.equal(writes.length, 1);
  assert.equal(writes[0].issue_number, 35);
});

test('unverifiable or unsuccessful workflow sources do not write comments', async () => {
  for (const run of [
    {head_sha: ''}, {head_sha: null}, {head_repository: null},
    {repository: {full_name: 'elsewhere/retrobar'}}, {event: 'push'}, {conclusion: 'failure'},
  ]) assert.deepEqual((await runFixture({run})).writes, []);
});

test('pagination finds the bot comment beyond the first page and ignores human markers', async () => {
  const comments = Array.from({length: 100}, (_, id) => ({id, user: {login: 'owner', type: 'User'}, body: 'Discussion'}));
  comments.push({id: 101, user: bot, body: marker + '\nOld downloads'});
  comments.push({id: 102, user: {login: 'owner', type: 'User'}, body: marker + '\nQuoted marker'});
  const {writes, observed} = await runFixture({comments});
  assert.equal(writes.length, 1);
  assert.equal(writes[0].type, 'update');
  assert.equal(writes[0].comment_id, 101);
  assert.ok(observed.some(args => args.issue_number === 35 && args.per_page === 100));
  const humanOnly = await runFixture({comments: [comments.at(-1)]});
  assert.equal(humanOnly.writes[0].type, 'create');
});

test('callbacks cannot replace a newer run or a newer attempt of the same run', async () => {
  for (const [id, attempt] of [[101, 1], [100, 3]]) {
    const comment = {id: 10, user: bot, body: `${marker}\n<!-- retrobar-artifacts-run: ${id} attempt: ${attempt} -->`};
    assert.deepEqual((await runFixture({comments: [comment]})).writes, []);
  }
});

test('expired or absent artifact sets preserve existing comments', async () => {
  for (const artifacts of [[], [{id: 1, name: 'Old', expired: true}]])
    assert.deepEqual((await runFixture({artifacts})).writes, []);
});
