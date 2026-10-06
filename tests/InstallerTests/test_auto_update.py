"""Detect actual installation identity before choosing an update destination."""
import importlib.util
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('auto_installer', ROOT / 'packaging/Installer/s1ds_installer.py')
i = importlib.util.module_from_spec(spec); spec.loader.exec_module(i)


def compressed(value):
    if value < 128: return bytes([value])
    if value < 16384: return bytes([0x80 | (value >> 8), value & 255])
    return bytes([0xc0 | (value >> 24), (value >> 16) & 255, (value >> 8) & 255, value & 255])


def serialized(value):
    raw = value.encode(); return compressed(len(raw)) + raw


def assembly(version='1.1.0-unn.7', branch='Beta', side='Client', noise=False, repository=None):
    """Minimal PE/CLI blob stream containing the same serialized assembly attributes."""
    bootstrap = 'DedicatedServerMod.Client.Core.ClientBootstrap' if side == 'Client' else 'DedicatedServerMod.Server.Core.ServerBootstrap'
    name = 'DedicatedServerClient' if side == 'Client' else 'DedicatedServerHost'
    melon = b'\x01\x00' + serialized(bootstrap) + serialized(name) + serialized(version) + serialized('Bars') + b'\xff\x00\x00'
    values = [melon]
    if branch: values.append(b'\x01\x00' + serialized('GameBranch') + serialized(branch) + b'\x00\x00')
    if repository: values.append(b'\x01\x00' + serialized('S1DSRepository') + serialized(repository) + b'\x00\x00')
    blob = b'\x00' + b''.join(compressed(len(v))+v for v in values)
    data = bytearray(1024 + len(blob)); data[:2] = b'MZ'; struct.pack_into('<I',data,60,128)
    data[128:132] = b'PE\x00\x00'; struct.pack_into('<H',data,134,1); struct.pack_into('<H',data,148,224)
    opt = 152; struct.pack_into('<H',data,opt,0x10b); struct.pack_into('<II',data,opt+96+14*8,0x2000,72)
    sec = opt+224; struct.pack_into('<IIII',data,sec+8,len(data)-512,0x2000,len(data)-512,512)
    struct.pack_into('<I',data,512,72); struct.pack_into('<II',data,520,0x2080,len(data)-640)
    meta = 640; data[meta:meta+4] = b'BSJB'; struct.pack_into('<I',data,meta+12,4); data[meta+16:meta+20] = b'v4\x00\x00'
    struct.pack_into('<H',data,meta+22,1); struct.pack_into('<II',data,meta+24,128,len(blob)); data[meta+32:meta+40]=b'#Blob\x00\x00\x00'
    data[meta+128:meta+128+len(blob)] = blob
    if noise: data[900:940] = b'1.1.0-unn.999 Beta upstream Public'.ljust(40,b'!')
    return bytes(data)


class AutoUpdateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name); self.game = self.root/'steamapps/common/Schedule I'; self.game.mkdir(parents=True)
        (self.game/'Schedule I.exe').write_bytes(b'game'); (self.game/'GameAssembly.dll').write_bytes(b'assembly')
        (self.game/'Mods').mkdir()
        self.settings={'repository':'UNN-Devotek/S1DedicatedServers','upstream_repository':'ifBars/S1DedicatedServers',
                       'games':{'public':{'build_id':'100'},'beta':{'build_id':'200'}}}
        self.mod=self.game/'Mods/DedicatedServerMod_Il2cpp_Client.dll'

    def test_manual_beta_fork_is_detected_without_receipt_or_network(self):
        self.mod.write_bytes(assembly(noise=True))
        m=i.detect_installation(self.game,self.settings)
        self.assertEqual((m['source'],m['channel'],m['runtime'],m['side'],m['version']),('fork','beta','Il2cpp','Client','1.1.0-unn.7'))

    def test_manual_upstream_and_server_mono_are_detected(self):
        self.mod.unlink(missing_ok=True)
        path=self.game/'Mods/DedicatedServerMod_Mono_Server.dll';path.write_bytes(assembly('1.1.0',None,'Server'))
        m=i.detect_installation(self.game,self.settings)
        self.assertEqual((m['source'],m['channel'],m['runtime'],m['side'],m['version']),('upstream','public','Mono','Server','1.1.0'))

    def test_matching_receipt_and_legacy_fork_receipt_preserve_selection(self):
        self.mod.write_bytes(b'recorded mod')
        home=self.game/i.STATE_DIR;home.mkdir()
        state={'schema':1,'files':{'Mods/'+self.mod.name:{'installed_sha256':i.sha256(self.mod)}},'source':'fork',
               'channel':'beta','runtime':'Il2cpp','side':'Client','version':'1.1.0-unn.8','tag':'beta-v1.1.0-unn.8'}
        for legacy in (False,True):
            if legacy:state.pop('source')
            i.write_json(home/'state.json',state)
            m=i.detect_installation(self.game,self.settings)
            self.assertEqual((m['source'],m['channel'],m['version']),('fork','beta','1.1.0-unn.8'))

    def test_stale_receipt_does_not_override_the_current_dll(self):
        self.mod.write_bytes(assembly('1.1.0',None))
        home=self.game/i.STATE_DIR;home.mkdir()
        i.write_json(home/'state.json',{'schema':1,'files':{'Mods/'+self.mod.name:{'installed_sha256':'0'*64}},
                                      'source':'fork','channel':'beta','runtime':'Il2cpp','side':'Client','version':'1.1.0-unn.8'})
        m=i.detect_installation(self.game,self.settings)
        self.assertEqual((m['source'],m['channel'],m['version']),('upstream','public','1.1.0'))

    def test_new_install_infers_game_beta_and_runtime(self):
        (self.game.parent.parent/'appmanifest_3164500.acf').write_text('"BetaKey" "beta" "buildid" "200"')
        m=i.detect_installation(self.game,self.settings)
        self.assertEqual((m['source'],m['channel'],m['runtime'],m['side']),('fork','beta','Il2cpp','Client'))

    def test_unknown_or_duplicate_dlls_are_not_guessed(self):
        self.mod.write_bytes(b'not a DLL')
        with self.assertRaisesRegex(ValueError,'detect|identify'):i.detect_installation(self.game,self.settings)
        self.mod.write_bytes(assembly());(self.game/'Mods/DedicatedServerMod_Mono_Client.dll').write_bytes(assembly())
        with self.assertRaisesRegex(ValueError,'Multiple'):i.detect_installation(self.game,self.settings)

    def test_explicit_selection_can_repair_unknown_metadata(self):
        self.mod.write_bytes(b'locally modified')
        m=i.detect_installation(self.game,self.settings,allow_unknown=True)
        self.assertEqual((m['runtime'],m['side']),('Il2cpp','Client'))
        self.assertIsNone(m['version'])

    def test_truncated_pe_and_false_strings_do_not_identify_a_mod(self):
        for data in (assembly()[:150], b'garbage 1.1.0-unn.7 GameBranch Beta', b'MZ'+b'\0'*100):
            self.mod.write_bytes(data)
            with self.subTest(length=len(data)),self.assertRaises(ValueError):i.detect_installation(self.game,self.settings)

    def test_filename_and_melon_side_mismatch_is_rejected(self):
        self.mod.write_bytes(assembly(side='Server'))
        with self.assertRaises(ValueError):i.detect_installation(self.game,self.settings)

    def test_repository_metadata_identifies_the_fork_without_a_version_suffix(self):
        self.mod.write_bytes(assembly('1.1.0','Public',repository=self.settings['repository']))
        m=i.detect_installation(self.game,self.settings)
        self.assertEqual((m['source'],m['channel']),('fork','public'))

    def test_unknown_custom_source_needs_explicit_selection(self):
        for data in (assembly('1.1.0-custom.2'),assembly(repository='another/custom-fork')):
            self.mod.write_bytes(data)
            with self.assertRaisesRegex(ValueError,'custom'):i.detect_installation(self.game,self.settings)
            self.assertEqual(i.detect_installation(self.game,self.settings,allow_unknown=True)['side'],'Client')

    def test_missing_recorded_mod_repairs_the_previous_beta_install(self):
        home=self.game/i.STATE_DIR;home.mkdir()
        i.write_json(home/'state.json',{'schema':1,'files':{'Mods/'+self.mod.name:{'installed_sha256':'0'*64}},
                                      'source':'fork','channel':'beta','runtime':'Il2cpp','side':'Client','version':'1.1.0-unn.8'})
        m=i.detect_installation(self.game,self.settings)
        self.assertEqual((m['source'],m['channel']),('fork','beta'))
        self.assertIsNone(m['version'])

    def test_version_comparison_orders_numeric_fork_revisions(self):
        self.assertGreater(i.version_key('1.1.0-unn.10'),i.version_key('1.1.0-unn.9'))
        self.assertGreater(i.version_key('1.2.0'),i.version_key('1.1.0-unn.99'))
        self.assertGreater(i.version_key('1.1.0'),i.version_key('1.1.0-rc.1'))

    def update(self, current='1.1.0-unn.7', target='1.1.0-unn.9', flags=(), recorded=False):
        self.mod.write_bytes(assembly(current))
        (self.game/'MelonLoader/net6').mkdir(parents=True)
        (self.game/'MelonLoader/net6/MelonLoader.dll').write_bytes(b'loader')
        (self.game/'version.dll').write_bytes(b'loader')
        self.settings['loader']={'version':'0.7.2'}
        i.write_json(self.root/'installer-settings.json',self.settings)
        payload=self.root/self.mod.name;payload.write_bytes(assembly(target))
        manifest={'source':'fork','channel':'beta','version':target,'tag':'beta-v'+target,
                  'game':{'build_id':'200'},'loader':self.settings['loader'],'runtime':'Il2cpp','side':'Client'}
        if recorded:
            original=self.root/'original'/self.mod.name;original.parent.mkdir();original.write_bytes(self.mod.read_bytes())
            i.install(self.game,dict(manifest,version=current),original,self.settings)
        before=self.mod.read_bytes()
        with patch.object(i,'ROOT',self.root),patch.object(i,'check_game'),patch.object(i,'prepare',return_value=(manifest,payload)) as prepare:
            result=i.main(['update','--game-directory',str(self.game),*flags])
        return result,prepare,before

    def test_update_automatically_fetches_beta_without_reusing_old_tag(self):
        result,prepare,_=self.update()
        self.assertEqual(result,0)
        args=prepare.call_args.args
        self.assertEqual(args[1:4],('beta','Il2cpp','Client'))
        self.assertIsNone(args[5]);self.assertEqual(args[7],'fork')
        self.assertEqual(i.load_state(self.game)['version'],'1.1.0-unn.9')

    def test_automatic_update_does_not_downgrade_a_newer_local_build(self):
        _,_,before=self.update(current='1.1.0-unn.10')
        self.assertEqual(self.mod.read_bytes(),before)
        self.assertFalse((self.game/i.STATE_DIR/'state.json').exists())

    def test_explicit_tag_allows_a_deliberate_downgrade(self):
        _,prepare,_=self.update(current='1.1.0-unn.10',flags=('--tag','beta-v1.1.0-unn.9'))
        self.assertEqual(prepare.call_args.args[5],'beta-v1.1.0-unn.9')
        self.assertEqual(i.load_state(self.game)['version'],'1.1.0-unn.9')

    def test_current_receipt_skips_rewriting_game_files(self):
        with patch.object(i,'install',wraps=i.install) as install:
            _,_,before=self.update(current='1.1.0-unn.9',recorded=True)
        self.assertEqual(install.call_count,1,'Only fixture setup should install; update must skip identical files')
        self.assertEqual(self.mod.read_bytes(),before)


if __name__=='__main__':unittest.main()
