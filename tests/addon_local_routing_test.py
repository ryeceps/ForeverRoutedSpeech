"""Execute the shipped Lua modules, including actual inbox callbacks, without game input."""
import json
import sys
import zlib
import xml.etree.ElementTree as ET
import os
import subprocess
import tempfile
from pathlib import Path
from lupa.lua51 import LuaRuntime

root=Path(__file__).resolve().parents[1]
lua=LuaRuntime(unpack_returned_tuples=True)
lua.execute(r'''
UIParent={}; frames={}; allFrames={}; SlashCmdList={}; notices={}; bindings={}; now=100
LE_PARTY_CATEGORY_INSTANCE=2; grouped=false; raided=false; instanced=false; guilded=true; channels={1,'General - Zone',false,2,'Trade - City',false,7,'Officers',false}
function GetTime() return now end
function IsInGuild() return guilded end
function IsInGroup(category) if category==2 then return instanced end; return grouped end
function IsInRaid() return raided end
function GetChannelList() return unpack(channels) end
function GetZoneText() return 'Ironforge' end
function GetSubZoneText() return 'The Commons' end
function IsResting() return true end
function InCombatLockdown() return false end
function IsControlKeyDown() return true end
function IsShiftKeyDown() return true end
function IsAltKeyDown() return true end
function GetCurrentKeyBoardFocus() return focused end
function SetOverrideBindingClick(owner,priority,key,button) bindings[key]=button end
function CreateFrame(kind,name)
 local f={kind=kind,name=name,text='',shown=true,scripts={},hooks={},attrs={},maxBytes=0,maxLetters=0}
 frames[name or ('anonymous'..(#allFrames+1))]=f; allFrames[#allFrames+1]=f
 function f:SetSize(w,h) self.width=w;self.height=h end
 function f:SetPoint(...) end; function f:SetAlpha(a) self.alpha=a end
 function f:SetAutoFocus(...) end; function f:SetMaxLetters(n) self.maxLetters=n end
 function f:SetFontObject(name) self.font=name end
 function f:SetMaxBytes(n) self.maxBytes=n end; function f:GetMaxBytes() return self.maxBytes end
 function f:GetMaxLetters() return self.maxLetters end
 function f:SetScript(key,fn) self.scripts[key]=fn end
 function f:GetScript(key) return self.scripts[key] end
 function f:HookScript(key,fn) self.hooks[key]=fn end
 function f:RegisterEvent(...) end
 function f:GetText() return self.text end
 function f:SetText(text)
  self.text=self.maxBytes>0 and text:sub(1,self.maxBytes) or text
  if self.maxLetters>0 and not self.text:find('[\128-\255]') then self.text=self.text:sub(1,self.maxLetters) end
  if self.scripts.OnTextChanged then self.scripts.OnTextChanged(self) end
  if self.hooks.OnTextChanged then self.hooks.OnTextChanged(self) end
 end
 function f:SetFocus() error('addon must never set focus') end; function f:ClearFocus() if focused==self then focused=nil end end
 function f:HasFocus() return focused==self end
 function f:IsShown() return self.shown end; function f:Hide() self.shown=false; self:ClearFocus() end
 function f:Show() error('addon must never show native UI') end; function f:IsObjectType(t) return self.kind==t end
 function f:GetAttribute(k) return self.attrs[k] end
 function f:SetAttribute(k,v) self.attrs[k]=v end
 function f:CreateTexture() error('Pixel bridge must not be created') end
 return f
end
chat=CreateFrame('EditBox','ChatFrame1EditBox'); chat.maxBytes=255; chat.maxLetters=255; chat.shown=false; chat.chatType='SAY'; chat.sticky='SAY'; sends=0; searchSends=0
function chat:GetChatType() return self.chatType end
function chat:SetChatType(t) self.chatType=t end
function chat:GetStickyType() return self.sticky end
function chat:GetChannelTarget() return self.channel end
function chat:SetChannelTarget(id) self.channel=id end
chat:SetScript('OnEnterPressed',function(self)
 if blockSend then error('protected client action') end
 sends=sends+1; sentText=self.text; sentType=self.chatType; sentChannel=self.channel
 self:SetText(''); self:Hide()
end)
search=CreateFrame('EditBox','AuctionSearch'); search:SetMaxLetters(63)
search:SetScript('OnEnterPressed',function() searchSends=searchSends+1 end)
DEFAULT_CHAT_FRAME={editBox=chat,AddMessage=function(_,s) notices[#notices+1]=s end}
ChatFrameUtil={GetActiveWindow=function() return chat.shown and chat or nil end,
 GetLastActiveWindow=function() return chat end,OpenChat=function() error('addon must never open native chat') end,
 UpdateHeader=function() end}
function reset()
 grouped=false;raided=false;instanced=false;guilded=true;blockSend=false;focused=nil
 channels={1,'General - Zone',false,2,'Trade - City',false,7,'Officers',false}
 chat.text='';chat.shown=false;chat.chatType='SAY';chat.sticky='SAY';chat.channel=nil;chat:SetMaxBytes(255)
 search.text='';search.shown=true;search:SetMaxBytes(0);search:SetMaxLetters(63)
 sends=0;searchSends=0;sentText=nil
end
''')
for file in ('LocalRouter.lua','Inbox.lua'):
    lua.execute((root/'addon/VoiceRouter'/file).read_text(encoding='utf-8'))
lua.execute("for _,f in ipairs(allFrames) do if f.kind=='Frame' then adapter=f end end; adapter.scripts.OnEvent(nil,'PLAYER_LOGIN')")
counter=0

def control(text,hint='default',nonce=None):
    global counter
    if nonce is None: counter+=1; nonce=counter
    header=f'frs2 {nonce:032x} {hint} {len(text.encode("utf-8"))}'
    checksum=zlib.adler32((header+' '+text).encode('utf-8'))
    return (header+f' {checksum:08x}').encode('ascii').hex()
def signal(number): lua.execute(f'frames.ForeverRoutedSpeechControl{number}.scripts.OnClick()')
def arm(hex):
    signal(17)
    for nibble in hex: signal(int(nibble,16)+1)
    signal(18)
def tick(seconds=.15): lua.execute(f'now=now+{seconds};adapter.scripts.OnUpdate()')
def manual_chat(): lua.execute("chat.shown=true;focused=chat;adapter.scripts.OnUpdate()")
def paste(text,settle=True):
    lua.globals().raw=text
    lua.execute('focused:SetText(raw)')
    if settle: tick()
def reset(): lua.execute("reset();adapter.scripts.OnEvent(nil,'PLAYER_LOGIN')")
def deliver(text,hint='default'):
    if not lua.eval('focused~=nil'): manual_chat() # player's native open action, not addon code
    arm(control(text,hint));paste(text)
def check(kind,text,channel=None):
    assert lua.globals().chat.chatType==kind and lua.globals().chat.text==text
    assert lua.globals().sends==0
    if channel is not None: assert lua.globals().chat.channel==channel

source=(root/'addon/VoiceRouter/Inbox.lua').read_text()
for forbidden in ('SetFocus','ClearFocus','OpenChat','OnEnterPressed','SlashCmdList','CreateFrame("EditBox"'):
    assert forbidden not in source, f'forbidden native UI path: {forbidden}'
assert lua.globals().frames['ForeverRoutedSpeechInbox'] is None
assert lua.globals().frames['VoiceRouterStatusStrip'] is None
assert lua.globals().bindings['CTRL-ALT-SHIFT-F17']=='ForeverRoutedSpeechControl17'
assert lua.globals().bindings['CTRL-ALT-SHIFT-F19']=='ForeverRoutedSpeechControl19'
toc=(root/'addon/VoiceRouter/VoiceRouter.toc.in').read_text()
assert 'LocalRouter.lua' in toc and 'Inbox.lua' in toc and '\nVoiceRouter.lua' not in toc and 'SavedVariables:' not in toc
binding=ET.parse(root/'addon/VoiceRouter/Bindings.xml').getroot()
assert binding.tag=='{http://www.blizzard.com/wow/ui/}Bindings' and len(binding)==0
if os.name=='nt':
    with tempfile.TemporaryDirectory(prefix='frs-addon-install-') as folder:
        target=Path(folder)/'VoiceRouter';target.mkdir()
        for old in ('Bindings.xml','VoiceRouter.lua','Preview.lua'): (target/old).write_text('obsolete fixture')
        subprocess.run(['powershell','-NoProfile','-File',str(root/'scripts/Install-Addon.ps1'),'-AddOnsDirectory',folder,'-Interface','16001'],check=True,capture_output=True)
        for name in ('Bindings.xml','Inbox.lua','LocalRouter.lua'): assert (target/name).read_bytes()==(root/'addon/VoiceRouter'/name).read_bytes()
        assert not (target/'VoiceRouter.lua').exists() and not (target/'Preview.lua').exists()

# Exact regression: no addon-originated native focus/open operations are available.
reset();arm(control('Closed'));assert not lua.globals().chat.shown and lua.globals().focused is None
reset();deliver('Hey guys');check('SAY','Hey guys')
for flags,kind in [('grouped=true','PARTY'),('grouped=true;raided=true','RAID'),('grouped=true;instanced=true','INSTANCE_CHAT')]:
    reset();lua.execute(flags);deliver('Hey guys');check(kind,'Hey guys')
reset();lua.execute('grouped=true');deliver('Tell everyone around me we need help');check('SAY','we need help')
reset();deliver('In General, hey guys');check('CHANNEL','hey guys',1)
reset();lua.execute("channels={6,'General - Ironforge',false,8,'Trade - City',false}");deliver('Ask in trade selling potions');check('CHANNEL','selling potions',8)
reset();deliver('In Officers, meeting tonight');check('CHANNEL','meeting tonight',7)
reset();lua.execute('channels={}');deliver('In General, hello');assert lua.globals().chat.text==''
reset();lua.execute('guilded=false;grouped=true');deliver('Hello','i:guild');check('PARTY','Hello')
reset();deliver('In General, hi','m:guild');check('GUILD','hi')
reset();deliver('Hello','m:channel:7');check('CHANNEL','Hello',7)
for text in ('I mentioned the guild yesterday',"Don't tell guild we need help"):
    reset();lua.execute('grouped=true');deliver(text);check('PARTY',text)
reset();manual_chat();lua.execute("chat.chatType='GUILD'");deliver('Hello');check('GUILD','Hello')
reset();manual_chat();arm(control('Group changed'));lua.execute('grouped=true;raided=true');paste('Group changed');check('RAID','Group changed')
reset();lua.execute('focused=search');deliver('Tell guild Stormwind');assert lua.globals().search.text=='Tell guild Stormwind' and lua.globals().searchSends==0 and not lua.globals().chat.shown
for field,old in [('chat','Existing draft'),('search','Existing query')]:
    reset();lua.globals().old=old;lua.execute(f'{field}.shown=true;focused={field};{field}.text=old')
    arm(control('New text'));paste(old+'New text');assert lua.globals()[field].text==old
reset();manual_chat();text='In General, fragmented paste';arm(control(text))
for end in (1,5,len(text)):
    paste(text[:end],False);tick(.05);assert lua.globals().sends==0
    assert lua.eval('focused==chat')
tick();check('CHANNEL','fragmented paste',1)
reset();manual_chat();arm(control('Hello'));paste('Other');assert lua.globals().chat.text=='Other' and lua.globals().chat.chatType=='SAY'
reset();manual_chat();arm(control('Hello')[:-2]+'00');paste('Hello');assert lua.globals().sends==0
reset();manual_chat();arm(control('In General, hello'));tick(4);paste('In General, hello');check('SAY','In General, hello')
reset();manual_chat();arm(control('In General, cancelled'));signal(19);paste('In General, cancelled');check('SAY','In General, cancelled')
reset();manual_chat();arm(control('Moved'));lua.execute('focused=search');paste('Moved');assert lua.globals().chat.text=='' and lua.globals().search.text=='Moved'
# Focused native fields can consume global bindings; test the key-event dispatch too.
reset();manual_chat();encoded=control('In General, key-event route')
for number in [17]+[int(n,16)+1 for n in encoded]+[18]:
    lua.globals().control_key=f'F{number}'
    lua.execute('chat.hooks.OnKeyDown(chat,control_key)')
paste('In General, key-event route');check('CHANNEL','key-event route',1)
reset();deliver('Hello 世界');check('SAY','Hello 世界')
reset();manual_chat();lua.execute("function chat:IsForbidden() return true end");arm(control('Restricted'));assert lua.globals().chat.text=='';lua.execute('chat.IsForbidden=nil')
reset();manual_chat();arm(control('In General, blocked'))
before=len(lua.globals().notices)
lua.execute("adapter.scripts.OnEvent(nil,'ADDON_ACTION_FORBIDDEN','VoiceRouter','SetPreferredGamepadInteractTarget()')")
paste('In General, blocked');assert lua.globals().sends==0 and lua.globals().chat.chatType=='SAY'
assert all('draft ready' not in lua.globals().notices[n] for n in range(before+1,len(lua.globals().notices)+1))
# Native field caps remain untouched; oversized/truncated paste never gets a ready notice.
reset();lua.execute('focused=search');raw='x'*64;before=len(lua.globals().notices);deliver(raw)
assert lua.globals().search.maxLetters==63 and lua.globals().searchSends==0 and lua.globals().search.text=='x'*63
assert 'text ready' not in lua.globals().notices[len(lua.globals().notices)]
# Mock enforces bytes rather than letters; reproduce native truncation explicitly.
reset();manual_chat();lua.execute('chat:SetMaxBytes(3)');arm(control('Hello'));paste('Hello')
assert lua.globals().sends==0 and 'draft ready' not in lua.globals().notices[len(lua.globals().notices)]

vectors=[]
for index,(text,hint) in enumerate([('Hello 世界','default'),('In General, hey guys','default'),('Guildies hello','i:guild'),('Stormwind','m:channel:9999')],1):
    encoded=control(text,hint,index);decoded=lua.globals().VoiceRouterLocal.control(encoded)
    assert lua.globals().VoiceRouterLocal.matches(decoded,text)
    vectors.append(dict(text=text,hint=hint,nonce=f'{index:032x}',hex=encoded))
path=root/'tests/fixtures/addon-control.json'
if '--write-fixture' in sys.argv: path.write_text(json.dumps(vectors,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
else: assert json.loads(path.read_text(encoding='utf-8'))==vectors
print('PASS: focus-preserving adapter: zero native focus/open/send calls; live routes, field preservation, checksummed control/plain paste, corruption, cancellation, expiry, Unicode, limits, protected-action refusal and installer upgrade.')
