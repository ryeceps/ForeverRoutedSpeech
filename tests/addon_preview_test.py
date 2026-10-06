"""Read-only preview and native input-boundary tests; not Forever-client evidence."""
from pathlib import Path
from lupa.lua51 import LuaRuntime

root = Path(__file__).resolve().parents[1]
lua = LuaRuntime(unpack_returned_tuples=True)
lua.execute('''
UIParent={}; frames={}; active=nil; focus=nil; opens=0
function CreateFrame(kind,name)
 local f={visible=true,strings={}}
 function f:SetSize(...) end; function f:SetPoint(...) end
 function f:SetFrameStrata(...) end; function f:EnableMouse(...) end
 function f:Hide() self.visible=false end; function f:Show() self.visible=true end
 function f:SetScript(name,fn) self[name]=fn end
 function f:CreateTexture() return {SetAllPoints=function() end,SetColorTexture=function() end} end
 function f:CreateFontString()
  local s={text=''}; function s:SetPoint(...) end; function s:SetWidth(...) end
  function s:SetJustifyH(...) end; function s:SetWordWrap(...) end
  function s:SetText(text) self.text=text end
  self.strings[#self.strings+1]=s; return s
 end
 frames[#frames+1]=f; return f
end
function newEdit(text,name,kind)
 return {text=text,GetText=function(self) return self.text end,
 GetName=function() return name end,GetChatType=function() return kind end}
end
ChatFrameUtil={GetActiveWindow=function() return active end,
 OpenChat=function(text) opens=opens+1;active=newEdit(text,'ChatBox','PARTY') end}
function GetCurrentKeyBoardFocus() return focus end
function SendChatMessage() error('Preview must never send') end
function CopyToClipboard() error('Preview must never change clipboard') end
''')
lua.execute((root / 'tests/fixtures/legacy-preview.lua').read_text(encoding='utf-8'))
lua.execute("frames[2].OnUpdate(nil,.25)")
assert not lua.globals().frames[1].visible
lua.execute("VoiceRouter_OpenDraft()")
assert not lua.globals().frames[1].visible, 'large preview hidden by default'
lua.execute("VoiceRouterStripDB={previewEnabled=true}; VoiceRouter_OpenDraft()")
assert lua.globals().opens == 1
assert lua.globals().frames[1].visible
lua.execute("active.text='/p Hello é'; frames[2].OnUpdate(nil,.25)")
assert lua.globals().frames[1].strings[2].text == '/p Hello é'
assert lua.globals().active.text == '/p Hello é', 'preview preserves native input'
lua.execute("VoiceRouter_OpenDraft()")
assert lua.globals().opens == 1, 'existing chat not reopened or erased'
lua.execute("active.text='a|cffff0000b'; frames[2].OnUpdate(nil,.25)")
assert lua.globals().frames[1].strings[2].text == 'a||cffff0000b'
lua.execute("active=nil; focus=newEdit('sword','AuctionSearch'); VoiceRouterProbeDB={textFields={AuctionSearch={limit=63,units='chars'}}}; frames[2].OnUpdate(nil,.25)")
assert lua.globals().frames[1].strings[2].text == 'sword'
lua.execute("VoiceRouter_OpenDraft()")
assert lua.globals().opens == 1, 'search focus not replaced by chat'
lua.execute("focus=newEdit('private text','OtherBox'); frames[2].OnUpdate(nil,.25)")
assert not lua.globals().frames[1].visible, 'unregistered field not displayed'
assert lua.globals().frames[1].strings[2].text == '', 'closed preview clears text'
lua.execute("focus=nil; ChatFrameUtil=nil; function ChatEdit_GetActiveWindow() return active end; function ChatEdit_OpenChat(text) opens=opens+1;active=newEdit(text,'LegacyBox','SAY') end; VoiceRouter_OpenDraft()")
assert lua.globals().opens == 2 and lua.globals().frames[1].visible
lua.execute("function issecretvalue(value) return value=='secret' end; active.text='secret'; frames[2].OnUpdate(nil,.25)")
assert not lua.globals().frames[1].visible, 'secret text not processed'
lua.execute("issecretvalue=nil; active.GetText=function() error('API failure') end; frames[2].OnUpdate(nil,.25)")
assert not lua.globals().frames[1].visible, 'API failure hides preview'
print('PASS: Lua preview opens/preserves native focus, reflects entered text, handles search/legacy/secret/API failure, and never sends or accesses clipboard. Forever untested.')
