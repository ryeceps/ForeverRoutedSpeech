-- Invisible, one-way input inbox. The companion never reads game pixels or memory.
local R=VoiceRouterLocal
local inbox=CreateFrame("EditBox","ForeverRoutedSpeechInbox",UIParent)
inbox:SetSize(1,1); inbox:SetPoint("TOPLEFT",UIParent,"TOPLEFT",0,0); inbox:SetAlpha(0)
inbox:SetAutoFocus(false); inbox:SetMaxLetters(512)
inbox:SetFontObject("GameFontNormal"); inbox:Show()
if inbox.SetMaxBytes then inbox:SetMaxBytes(512) end
local pending,busy,seen=nil,false,{}
local seenOrder={}
local function notify(text)
    if DEFAULT_CHAT_FRAME and DEFAULT_CHAT_FRAME.AddMessage then DEFAULT_CHAT_FRAME:AddMessage("ForeverRoutedSpeech: "..text) end
end
local function activeChat()
    return type(ChatFrameUtil)=="table" and R.call(ChatFrameUtil.GetActiveWindow) or R.call(ChatEdit_GetActiveWindow)
end
local function focus() return R.call(GetCurrentKeyBoardFocus) end
local function isChat(edit)
    return edit and (edit==activeChat() or R.call(edit.GetChatType,edit)~=nil or R.call(edit.GetAttribute,edit,"chatType")~=nil)
end
local function lastChat()
    return activeChat() or (type(ChatFrameUtil)=="table" and R.call(ChatFrameUtil.GetLastActiveWindow)) or R.call(ChatEdit_GetLastActiveWindow) or
        (DEFAULT_CHAT_FRAME and DEFAULT_CHAT_FRAME.editBox)
end
local function cancel()
    local previous=pending and pending.focus
    pending=nil; busy=true; inbox:SetText(""); inbox:ClearFocus(); busy=false
    if previous and R.call(previous.IsShown,previous) then R.call(previous.SetFocus,previous) end
end
local function prepare()
    cancel()
    local current=focus()
    local blocked
    if current and type(current.GetText)=="function" and (R.call(current.GetText,current) or "")~="" then
        blocked="The current field contains text. Finish or clear it before dictation paste."
    end
    local search=current and not isChat(current) and R.call(current.IsObjectType,current,"EditBox")
    if current and not isChat(current) and not search then blocked="Unsupported focused control." end
    pending={focus=current,search=search and current or nil,chat=lastChat(),open=current and isChat(current),started=GetTime(),blocked=blocked}
    busy=true; inbox:SetText(""); busy=false; inbox:SetFocus()
end
local types={say="SAY",guild="GUILD",party="PARTY",raid="RAID",instance="INSTANCE_CHAT",general="CHANNEL",trade="CHANNEL",lookingforgroup="CHANNEL",custom="CHANNEL"}
local kinds={SAY="say",GUILD="guild",PARTY="party",RAID="raid",INSTANCE_CHAT="instance"}
local function selected(edit,context,open)
    if not edit then return nil end
    local kind=(not open and R.call(edit.GetStickyType,edit)) or R.call(edit.GetChatType,edit) or R.call(edit.GetAttribute,edit,"chatType")
    if kind=="CHANNEL" then
        local id=R.call(edit.GetChannelTarget,edit) or R.call(edit.GetAttribute,edit,"channelTarget")
        for _,c in ipairs(context.channels) do if c.id==id then return {kind=c.kind,channel=c,open=open} end end
    end
    return kinds[kind] and {kind=kinds[kind],open=open} or {unsupported=true,open=open}
end
local function setField(edit,text)
    local maxBytes=R.call(edit.GetMaxBytes,edit)
    local maxLetters=R.call(edit.GetMaxLetters,edit)
    local _,letters=text:gsub("[^\128-\191]","")
    if type(maxBytes)=="number" and maxBytes>0 and #text>maxBytes or type(maxLetters)=="number" and maxLetters>0 and letters>maxLetters then
        return nil,"Draft exceeds this field's limit. Edit it in the companion."
    end
    local ok=pcall(edit.SetText,edit,text)
    local actual=R.call(edit.GetText,edit)
    if not ok or actual~=text then
        if type(actual)=="string" and actual~="" and text:sub(1,#actual)==actual then R.call(edit.SetText,edit,"") end
        return nil,"The field did not accept the complete draft. Nothing was sent."
    end
    R.call(edit.SetFocus,edit)
    if not R.call(edit.HasFocus,edit) then return nil,"The field did not accept focus. Select it manually before sending." end
    return true
end
local function consume()
    if busy or not pending or not R.call(inbox.HasFocus,inbox) then return end
    local raw=inbox:GetText()
    if raw=="" then return end
    local transaction=pending
    if transaction.blocked then cancel(); notify(transaction.blocked); return end
    local packet,error=R.decode(raw)
    if not packet then cancel(); notify(error); return end
    if GetTime()-transaction.started>3 or seen[packet.nonce] then cancel(); notify("Expired or repeated draft. Nothing was sent."); return end
    seen[packet.nonce]=true; seenOrder[#seenOrder+1]=packet.nonce
    if #seenOrder>128 then seen[table.remove(seenOrder,1)]=nil end
    local context,decision
    if not transaction.search then
        context,error=R.context()
        if context then decision,error=R.resolve(packet,context,selected(transaction.chat,context,transaction.open)) end
        if not decision then cancel(); notify(error); return end
    end
    pending=nil; busy=true; inbox:SetText(""); inbox:ClearFocus(); busy=false
    local edit=transaction.search
    if edit then
        if not R.call(edit.IsShown,edit) or (R.call(edit.GetText,edit) or "")~="" then notify("Search field changed; paste stopped."); return end
        local ok,fieldError=setField(edit,packet.text)
        notify(ok and "Search draft ready. Confirm search with your gamepad." or fieldError)
        return -- Never submit a search, even when auto-send was requested.
    end
    if transaction.open then edit=transaction.chat end
    if not edit then
        if type(ChatFrameUtil)=="table" and type(ChatFrameUtil.OpenChat)=="function" then R.call(ChatFrameUtil.OpenChat,"")
        else R.call(ChatEdit_OpenChat,"") end
        edit=activeChat()
    end
    if not edit or not R.call(edit.IsShown,edit) or (R.call(edit.GetText,edit) or "")~="" then notify("Chat could not open with an empty field. Paste stopped."); return end
    local chatType=types[decision.kind]
    local ok=pcall(function()
        if edit.SetChatType then edit:SetChatType(chatType) else edit:SetAttribute("chatType",chatType) end
        if decision.channel then
            if edit.SetChannelTarget then edit:SetChannelTarget(decision.channel.id) else edit:SetAttribute("channelTarget",decision.channel.id) end
        end
        if type(ChatFrameUtil)=="table" and ChatFrameUtil.UpdateHeader then ChatFrameUtil.UpdateHeader(edit)
        elseif type(ChatEdit_UpdateHeader)=="function" then ChatEdit_UpdateHeader(edit) end
    end)
    if not ok then notify("Chat destination could not be set. Nothing was sent."); return end
    local appliedType=R.call(edit.GetChatType,edit) or R.call(edit.GetAttribute,edit,"chatType")
    local appliedId=R.call(edit.GetChannelTarget,edit) or R.call(edit.GetAttribute,edit,"channelTarget")
    if appliedType~=chatType or decision.channel and appliedId~=decision.channel.id then notify("Chat destination did not match. Nothing was sent."); return end
    local filled,fieldError=setField(edit,decision.message)
    if not filled then notify(fieldError); return end
    local name=decision.channel and decision.channel.name.." (/"..decision.channel.id..")" or decision.kind
    if not packet.send then notify(name.." draft ready. Press A to send."); return end
    -- Recheck immediately before invoking the game's own Enter handler. Never retry.
    if not R.call(edit.HasFocus,edit) or R.call(edit.GetText,edit)~=decision.message then notify("Chat focus/text changed. Send stopped."); return end
    local enter=R.call(edit.GetScript,edit,"OnEnterPressed")
    if type(enter)~="function" then notify("Auto-send unavailable on this client. Press A to send."); return end
    local submitted,why=pcall(enter,edit)
    if not submitted then notify("Auto-send blocked by the client. Review the draft and press A. "..tostring(why)); return end
    if R.call(edit.GetText,edit)==decision.message then notify("The client left the draft unsent. Review it and press A; no retry was made."); return end
    -- Never Escape or discard text blindly. Hide only an empty native edit box.
    if R.call(edit.GetText,edit)=="" then R.call(edit.ClearFocus,edit); R.call(edit.Hide,edit) end
    notify(name.." send requested once; server delivery is unconfirmed.")
end
-- Native pastes can emit multiple changes. Decode only after a quiet interval,
-- otherwise the first slash/header would cancel focus and lose the remaining text.
inbox:SetScript("OnTextChanged",function()
    if not busy and pending then pending.changed=GetTime() end
end)
inbox:SetScript("OnEscapePressed",cancel)
inbox:SetScript("OnUpdate",function()
    if pending and GetTime()-pending.started>3 then cancel(); notify("Draft inbox timed out. Nothing was sent.")
    elseif pending and pending.changed and GetTime()-pending.changed>=.1 then
        pending.changed=nil; consume()
    end
end)
local openButton=CreateFrame("Button","ForeverRoutedSpeechOpenInbox",UIParent)
openButton:SetScript("OnClick",prepare)
local cancelButton=CreateFrame("Button","ForeverRoutedSpeechCancelInbox",UIParent)
cancelButton:SetScript("OnClick",cancel)
local function bind()
    if R.call(InCombatLockdown) then return end
    if type(SetOverrideBindingClick)=="function" then
        SetOverrideBindingClick(openButton,true,"CTRL-SHIFT-F10","ForeverRoutedSpeechOpenInbox")
        SetOverrideBindingClick(cancelButton,true,"CTRL-SHIFT-F9","ForeverRoutedSpeechCancelInbox")
    else notify("Addon shortcut API unavailable on this client.") end
end
local hooked=setmetatable({},{__mode="k"})
local function hookKeys(edit)
    if not edit or hooked[edit] or type(edit.HookScript)~="function" then return end
    hooked[edit]=true
    edit:HookScript("OnKeyDown",function(_,key)
        if R.call(IsControlKeyDown) and R.call(IsShiftKeyDown) then
            if key=="F10" then prepare() elseif key=="F9" then cancel() end
        end
    end)
end
local events=CreateFrame("Frame")
events:RegisterEvent("PLAYER_LOGIN"); events:RegisterEvent("PLAYER_REGEN_ENABLED")
events:SetScript("OnEvent",function() bind(); hookKeys(lastChat()) end)
local elapsed=0
events:SetScript("OnUpdate",function(_,dt)
    elapsed=elapsed+dt
    if elapsed>=.25 then elapsed=0; hookKeys(focus()) end
end)
hookKeys(inbox)
-- Unknown addon commands remain local and never become public chat messages.
SLASH_FOREVERROUTEDSPEECH1="/frs1"
SlashCmdList.FOREVERROUTEDSPEECH=function() notify("Use the final controller click to deliver the draft. No message was sent.") end
SLASH_VOICEROUTER1="/wvr"
SlashCmdList.VOICEROUTER=function(text)
    notify("Addon routing ready. Context is resolved here at delivery. No strip, calibration or setup commands.")
end
