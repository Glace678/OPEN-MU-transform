import { hapTasks } from '@ohos/hvigor-ohos-plugin';
import * as fs from 'fs';
import * as path from 'path';
import * as JSON5 from 'json5';
import { validateMobilePairing } from '../../build-tools/mobile-pairing.cjs';

const profile = JSON5.parse(fs.readFileSync(path.resolve(__dirname, 'build-profile.json5'), 'utf8'));
validateMobilePairing(profile, 'game', process.env.OPENMU_ALLOW_PLACEHOLDER_MOBILE_KEY === '1');

export default {
  system: hapTasks,
  plugins: []
};
