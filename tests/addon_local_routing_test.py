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
lua.execute('''
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
  if self.scripts.OnTextChanged then self.scripts.OnTextChanged(self) end
 end
 function f:SetFocus() focused=self end; function f:ClearFocus() if focused==self then focused=nil end end
 function f:HasFocus() return focused==self end
 function f:IsShown() return self.shown end; function f:Hide() self.shown=false; self:ClearFocus() end
 function f:Show() self.shown=true end; function f:IsObjectType(t) return self.kind==t end
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
 GetLastActiveWindow=function() return chat end,OpenChat=function() chat:Show();chat:SetFocus() end,
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
lua.execute("allFrames[#allFrames].scripts.OnEvent(nil,'PLAYER_LOGIN')")
counter=0
fixtures=[]
def packet(text,hint='default',send=False,nonce=None):
    global counter
    if nonce is None: counter+=1; nonce=counter
    b=text.encode('utf-8')
    protected=f'/frs1 {nonce:032x} {hint} {int(send)} {len(b)} {text}'.encode('utf-8')
    return f'/frs1 {nonce:032x} {hint} {int(send)} {len(b)} {zlib.adler32(protected):08x} {text}'
def prepare():
    lua.execute("frames.ForeverRoutedSpeechOpenInbox.scripts.OnClick()")
def paste(raw,settle=True):
    lua.globals().raw=raw
    lua.execute("frames.ForeverRoutedSpeechInbox:SetText(raw)")
    if settle: lua.execute('now=now+.15; frames.ForeverRoutedSpeechInbox.scripts.OnUpdate()')
def deliver(text,hint='default',send=False):
    prepare();raw=packet(text,hint,send);paste(raw);return raw
def reset(): lua.execute('reset()')
def check_chat(kind,text,channel=None):
    assert lua.globals().chat.chatType==kind and lua.globals().chat.text==text
    if channel is not None: assert lua.globals().chat.channel==channel
    assert lua.globals().sends==0

assert 'OnEnterPressed' not in (root/'addon/VoiceRouter/Inbox.lua').read_text(), 'addon must never invoke native send'
inbox=lua.globals().frames['ForeverRoutedSpeechInbox']
assert inbox.alpha==0 and inbox.width==1 and inbox.height==1
assert inbox.font=='GameFontNormal' and inbox.shown, 'hidden-by-alpha inbox still has native font and is shown for keyboard focus'
assert lua.globals().frames['VoiceRouterStatusStrip'] is None
assert lua.globals().bindings['CTRL-SHIFT-F10']=='ForeverRoutedSpeechOpenInbox'
toc=(root/'addon/VoiceRouter/VoiceRouter.toc.in').read_text()
assert 'LocalRouter.lua' in toc and 'Inbox.lua' in toc and '\nVoiceRouter.lua' not in toc
assert '\nPreview.lua' not in toc and 'SavedVariables:' not in toc
bindings_xml=ET.parse(root/'addon/VoiceRouter/Bindings.xml').getroot()
assert bindings_xml.tag=='{http://www.blizzard.com/wow/ui/}Bindings' and len(bindings_xml)==0, 'loader compatibility file must contain no bindings'
if os.name=='nt':
    with tempfile.TemporaryDirectory(prefix='frs-addon-install-') as folder:
        target=Path(folder)/'VoiceRouter'; target.mkdir()
        for old in ('Bindings.xml','VoiceRouter.lua','Preview.lua'):
            (target/old).write_text('obsolete fixture',encoding='utf-8')
        subprocess.run(['powershell','-NoProfile','-File',str(root/'scripts/Install-Addon.ps1'),
                        '-AddOnsDirectory',folder,'-Interface','16001'],check=True,capture_output=True)
        for name in ('Bindings.xml','Inbox.lua','LocalRouter.lua'):
            assert (target/name).read_bytes()==(root/'addon/VoiceRouter'/name).read_bytes(), name
        assert not (target/'VoiceRouter.lua').exists() and not (target/'Preview.lua').exists()
        assert 'Bindings.xml' not in (target/'VoiceRouter.toc').read_text(encoding='utf-8-sig')
context=lua.globals().VoiceRouterLocal.context()

# A real edit box may emit several OnTextChanged callbacks during one paste.
# Never reject the initial slash/header before the rest of the packet arrives.
reset();prepare();fragmented=packet('In General, fragmented paste',send=True)
for end in (1,12,60,len(fragmented)):
    paste(fragmented[:end],settle=False)
    lua.execute('now=now+.05; frames.ForeverRoutedSpeechInbox.scripts.OnUpdate()')
    assert lua.eval('focused==frames.ForeverRoutedSpeechInbox'), 'partial paste must retain inbox focus until settled'
    assert lua.globals().sends==0
lua.execute('now=now+.15; frames.ForeverRoutedSpeechInbox.scripts.OnUpdate()')
assert lua.globals().sends==0 and lua.globals().chat.text=='fragmented paste'
assert context.zone=='Ironforge' and context.city and context.resting
lua.execute("function GetZoneText() return 'Goldshire' end")
context=lua.globals().VoiceRouterLocal.context()
assert context.city is None and context.resting, 'resting in an inn does not imply a city'
lua.execute("function GetZoneText() return 'Ironforge' end")
reset(); deliver('Hey guys');check_chat('SAY','Hey guys')
reset(); lua.execute('grouped=true');deliver('Hey guys');check_chat('PARTY','Hey guys')
reset();lua.execute('grouped=true;raided=true');deliver('Hey guys');check_chat('RAID','Hey guys')
reset();lua.execute('grouped=true;raided=true;instanced=true');deliver('Hey guys');check_chat('INSTANCE_CHAT','Hey guys')
reset();lua.execute('grouped=true');deliver('Tell everyone around me we need help');check_chat('SAY','we need help')
reset();deliver('In General, hey guys');check_chat('CHANNEL','hey guys',1)
reset();lua.execute("channels={6,'General - Ironforge',false,8,'Trade - City',false}")
deliver('Ask in trade selling potions');check_chat('CHANNEL','selling potions',8)
reset();deliver('In Officers, meeting tonight');check_chat('CHANNEL','meeting tonight',7)
reset();lua.execute("channels={}");deliver('In General, hey guys',send=True)
assert lua.globals().sends==0 and lua.globals().chat.text=='' and not lua.globals().chat.shown
reset();lua.execute('guilded=false;grouped=true');deliver('Tell guild hello',send=True)
assert lua.globals().sends==0 and not lua.globals().chat.shown
reset();lua.execute('guilded=false;grouped=true');deliver('Hello friends','i:guild');check_chat('PARTY','Hello friends')
reset();lua.execute('grouped=true');deliver("I mentioned the guild yesterday");check_chat('PARTY','I mentioned the guild yesterday')
reset();lua.execute('grouped=true');deliver("Don't tell guild we need help");check_chat('PARTY',"Don't tell guild we need help")
reset();deliver('In General, hi','m:guild');check_chat('GUILD','hi')
reset();deliver('Hello','m:channel:7');check_chat('CHANNEL','Hello',7)
reset();lua.execute("grouped=true; chat.shown=true; chat.chatType='GUILD';focused=chat")
deliver('Hello');check_chat('GUILD','Hello')
reset();lua.execute("chat.shown=true;chat.chatType='WHISPER';focused=chat")
deliver('Hello',send=True);assert lua.globals().sends==0 and lua.globals().chat.text==''
reset();prepare();lua.execute('grouped=true;raided=true');paste(packet('Group changed'));check_chat('RAID','Group changed')
reset();lua.execute("focused=search");deliver('Stormwind',send=True)
assert lua.globals().search.text=='Stormwind' and lua.globals().searchSends==0 and lua.globals().sends==0 and not lua.globals().chat.shown
reset();lua.execute("chat.shown=true;focused=chat;chat.text='Existing draft'");deliver('New text',send=True)
assert lua.globals().chat.text=='Existing draft' and lua.globals().sends==0
reset();lua.execute("focused=search;search.text='Existing query'");deliver('New query',send=True)
assert lua.globals().search.text=='Existing query' and lua.globals().searchSends==0
reset();lua.execute('focused=search');deliver('a'*64);assert lua.globals().search.text==''
reset();prepare();paste(packet('Hello')[:-1]);assert lua.globals().sends==0 and not lua.globals().chat.shown
reset();prepare();paste(packet('Hello').replace(' default 0 ',' default 1 '));assert lua.globals().sends==0 and not lua.globals().chat.shown
reset();prepare();lua.execute('now=now+4; frames.ForeverRoutedSpeechInbox.scripts.OnUpdate()');paste(packet('Late',send=True))
assert lua.globals().sends==0 and not lua.globals().chat.shown
reset();prepare();lua.execute("frames.ForeverRoutedSpeechCancelInbox.scripts.OnClick()");paste(packet('Cancelled',send=True))
assert lua.globals().sends==0 and not lua.globals().chat.shown
reset();raw=deliver('In General, hello',send=True)
assert lua.globals().sends==0 and lua.globals().chat.text=='hello' and lua.globals().chat.channel==1 and lua.globals().chat.shown
reset();prepare();paste(raw);assert lua.globals().sends==0
reset();lua.execute("focused=search; function search:IsForbidden() return true end");deliver('Restricted',send=True)
assert lua.globals().search.text=='' and lua.globals().sends==0
lua.execute('search.IsForbidden=nil')
lua.execute("allFrames[#allFrames].scripts.OnEvent(nil,'ADDON_ACTION_FORBIDDEN','VoiceRouter','SendChatMessage()')")
assert 'SendChatMessage()' in lua.globals().notices[len(lua.globals().notices)]
reset();lua.execute('blockSend=true');deliver('Hello',send=True)
assert lua.globals().sends==0 and lua.globals().chat.text=='Hello' and lua.globals().chat.shown
reset();deliver('Hello 世界');check_chat('SAY','Hello 世界')
reset();lua.execute('chat:SetMaxBytes(3)');deliver('Hello',send=True)
assert lua.globals().sends==0 and lua.globals().chat.text==''
reset();lua.execute("chat:SetMaxBytes(3); function chat:GetMaxBytes() return 0 end")
deliver('Hello',send=True);assert lua.globals().sends==0 and lua.globals().chat.text=='', 'unexpected truncation rolls back only owned partial text'
lua.execute("function chat:GetMaxBytes() return self.maxBytes end")
reset();lua.execute('IsInGuild=nil');deliver('No API',send=True);assert lua.globals().sends==0
print('PASS: actual Lua inbox and routing: no visible strip, group transitions, live channel IDs, explicit/custom/manual routes, unavailable targets, search, occupied fields, corruption, expiry, cancellation, replay, Unicode and manual-only send (including legacy send flags). Live Forever APIs remain unverified.')

# Cross-language vectors: C# must emit exactly the packets Lua accepts.
for index,(text,hint,send) in enumerate([('Hello 世界','default',False),('In General, hey guys','default',True),('Guildies hello','i:guild',False)],1):
    raw=packet(text,hint,send,nonce=index)
    decoded=lua.globals().VoiceRouterLocal.decode(raw)
    if isinstance(decoded,tuple): decoded=decoded[0]
    assert decoded.text==text
    fixtures.append(dict(text=text,hint=hint,send=send,nonce=f'{index:032x}',packet=raw))
path=root/'tests/fixtures/addon-envelope.json'
if '--write-fixture' in sys.argv: path.write_text(json.dumps(fixtures,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
else: assert json.loads(path.read_text(encoding='utf-8'))==fixtures,'Addon envelope fixture must be reviewed/regenerated'
