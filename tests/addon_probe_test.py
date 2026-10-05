"""Run the addon in Lua 5.1 mocks; verify its exact wire bytes, not client support."""
import json
import math
import struct
import sys
import zlib
from pathlib import Path
from lupa.lua51 import LuaRuntime

root=Path(__file__).resolve().parents[1]
lua=LuaRuntime(unpack_returned_tuples=True)
lua.execute('''
drawCalls=0; pixels = {}; handlers = {}; SlashCmdList = {}; UIParent = {GetEffectiveScale=function() return .75 end}; LE_PARTY_CATEGORY_INSTANCE = 2
function CreateFrame()
 local f = {}; strip=f
 function f:SetScale(scale) self.scale=scale end
 function f:SetSize(w,h) self.width=w; self.height=h end
 function f:SetPoint(...) self.point={...} end; function f:SetFrameStrata(...) end
 function f:SetClampedToScreen(...) end; function f:ClearAllPoints() end
 function f:GetPoint() return 'TOPLEFT', UIParent, 'TOPLEFT', 50, -80 end
 function f:EnableMouse(...) end; function f:SetMovable(...) end; function f:RegisterForDrag(...) end
 function f:StartMoving() end; function f:StopMovingOrSizing() end; function f:RegisterEvent(...) end
 function f:Hide() end; function f:Show() end
 function f:SetScript(name, fn) handlers[name] = fn end
 function f:CreateTexture()
  local t = {}; function t:SetSize(...) end; function t:SetPoint(...) end
  function t:SetColorTexture(r,g,b,a) self.r = r; drawCalls=drawCalls+1 end
  pixels[#pixels+1] = t; return t
 end
 function f:CreateFontString() return {SetPoint=function() end,SetText=function() end,Hide=function() end,Show=function() end} end
 return f
end
function GetTime() return 123 end
function GetBuildInfo() return 'Forever fixture', '12345', '', 99999 end
function IsInGuild() return true end
function IsInGroup(category) return category ~= 2 end
function IsInRaid() return false end
function GetChannelList() return 4, 'Trade - City', false, 9, 'Friends é', false end
SLASH_SAY1 = '/say'; SLASH_GUILD1 = '/g'; SLASH_PARTY1 = '/p'; SLASH_RAID1 = '/raid'; SLASH_INSTANCE_CHAT1 = '/i'
function print(...) end
''')
lua.execute((root/"addon/VoiceRouter/VoiceRouter.lua").read_text(encoding="utf-8"))
lua.execute("handlers.OnEvent(nil,'ADDON_LOADED','VoiceRouter'); handlers.OnUpdate(nil,.25)")
assert lua.globals().strip.width==128 and lua.globals().strip.height==32, 'compact strip dimensions'
assert abs(lua.globals().strip.scale*.75-1)<1e-9, 'physical pixel scale compensation'

def frame():
    values=[int(lua.globals().pixels[i].r) for i in range(1,4097)]
    return bytes(sum(values[n*8+k]<<k for k in range(8)) for n in range(512))
def decode(data):
    assert data[:4]==b"WVR1"
    length,session,sequence=struct.unpack_from("<HII",data,4)
    assert zlib.adler32(data[:14+length])==struct.unpack_from("<I",data,14+length)[0]
    return data[14:14+length].decode("utf-8").split("\t"),sequence
before=lua.globals().drawCalls
lua.execute("handlers.OnUpdate(nil,.25)")
assert 0 < lua.globals().drawCalls-before < 128, 'only changed heartbeat/checksum pixels redraw'
lua.execute("handlers.OnDragStop({StopMovingOrSizing=function() end,GetPoint=function() return 'TOPLEFT',UIParent,'TOPLEFT',50,-80 end})")
assert lua.globals().VoiceRouterStripDB.x==50 and lua.globals().VoiceRouterStripDB.y==-80, 'drag persists position'
lua.execute("handlers.OnEvent(nil,'ADDON_LOADED','VoiceRouter')")
assert lua.globals().strip.point[4]==50 and lua.globals().strip.point[5]==-80, 'saved position restored on load'
fields,sequence=decode(frame());assert fields[6]=="" and fields[4]=="0", "unverified disabled"
lua.execute("SlashCmdList.VOICEROUTER('rendered'); SlashCmdList.VOICEROUTER('verify say'); SlashCmdList.VOICEROUTER('verify party'); SlashCmdList.VOICEROUTER('verify custom'); SlashCmdList.VOICEROUTER('limit bytes 255'); handlers.OnUpdate(nil,.25)")
data=frame();fields,advanced=decode(data)
assert advanced>sequence and fields[2]=="party" and fields[4]=="255"
assert "Say=/say" in fields[6] and "Custom=numbered" in fields[6]
assert "9,Custom,Friends %C3%A9" in fields[7]
out=root/"artifacts";out.mkdir(exist_ok=True);(out/"addon-frame.bin").write_bytes(data)
lua.execute("IsInGuild=nil; handlers.OnUpdate(nil,.25)");assert frame()==data,"missing API freezes heartbeat"
lua.execute("function IsInGuild() return true end; function GetChannelList() return 1,'Unexpected shape' end; handlers.OnUpdate(nil,.25)")
assert frame()==data,"unknown API shape freezes heartbeat"
lua.execute("function GetChannelList() return 7,'Trade - City',false end; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert "7,Trade" in fields[7],"renumbering reflected"
lua.execute("function GetChannelList() return 7,'TradeFriends',false end; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert "7,Custom" in fields[7],"custom name not public"
lua.execute("SlashCmdList.VOICEROUTER('reset'); handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[6]=="" and fields[4]=="0"
lua.execute("function ChatEdit_GetActiveWindow() return nil end; SlashCmdList.VOICEROUTER('chatinput'); handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[8]=="closed"
lua.execute("function ChatEdit_GetActiveWindow() return {} end; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[8]=="open"
lua.execute("ChatEdit_GetActiveWindow=nil; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[8]=="unknown"
lua.execute("search={GetName=function() return 'AuctionSearch' end}; function GetCurrentKeyBoardFocus() return search end; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[0]=='4' and fields[9]=='unsupported' and fields[10]=='AuctionSearch'
lua.execute("SlashCmdList.VOICEROUTER('field chars 63'); handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[9]=='auctionhouse' and fields[11]=='63'
lua.execute("function GetCurrentKeyBoardFocus() return {GetName=function() return 'OtherBox' end} end; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[9]=='unsupported', 'other text fields never inherit approval'
lua.execute("function GetCurrentKeyBoardFocus() return search end; function ChatEdit_GetActiveWindow() return search end; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[9]=='none', 'chat focus not mistaken for search'
lua.execute("SlashCmdList.VOICEROUTER('reset'); ChatEdit_GetActiveWindow=nil; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[9]=='unsupported', 'reset clears field approval'
lua.execute("ChatFrameUtil={GetActiveWindow=function() return search end}; SlashCmdList.VOICEROUTER('chatinput'); handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[8]=='open' and fields[9]=='none', 'modern chat API recognized without legacy alias'
lua.execute("search.GetAttribute=function(self,key) if key=='chatType' then return 'PARTY' end end; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[13]=='Party' and fields[14]==''
lua.execute("search.GetAttribute=function(self,key) if key=='chatType' then return 'CHANNEL' else return 7 end end; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[13]=='Custom' and fields[14]=='7'
lua.execute("search.GetAttribute=function(self,key) return 'WHISPER' end; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[13]=='unsupported'
lua.execute("otherSearch={GetName=function() return 'InventorySearch' end}; GetCurrentKeyBoardFocus=function() return otherSearch end; handlers.OnUpdate(nil,.25); SlashCmdList.VOICEROUTER('field chars 40'); handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[9]=='search' and fields[10]=='InventorySearch'
lua.execute("VoiceRouterProbeDB.chatInputVerified=nil; GetCurrentKeyBoardFocus=function() return search end; search.GetAttribute=function() return 'SAY' end; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[8]=='open' and fields[13]=='Say', 'matching live keyboard and chat focus does not need manual flag'
lua.execute("GetCurrentKeyBoardFocus=function() return nil end; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[8]=='closed' and fields[13]=='none', 'unfocused active chat never enables paste'
lua.execute("GetCurrentKeyBoardFocus=function() error('focus failure') end; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[8]=='unknown', 'focus API failure blocks paste'
lua.execute("SlashCmdList.VOICEROUTER('preview on')")
assert lua.globals().VoiceRouterStripDB.previewEnabled
lua.execute("SlashCmdList.VOICEROUTER('preview off')")
assert not lua.globals().VoiceRouterStripDB.previewEnabled
lua.execute("GetCurrentKeyBoardFocus=function() return search end; search.GetText=function() return 'Hello é' end; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[15]=='8' and int(fields[16])==zlib.adler32('Hello é'.encode('utf-8')), 'input echo byte count and UTF8 checksum'
lua.execute("issecretvalue=function() return true end; handlers.OnUpdate(nil,.25)")
fields,_=decode(frame());assert fields[15]=='-1' and fields[16]=='', 'secret field text never echoed'
print("PASS: Lua 5.1 framing, checksum, capability gates, modern/legacy chat focus, heartbeat, renumbering, UTF-8 and API failures. Actual Forever client untested.")
