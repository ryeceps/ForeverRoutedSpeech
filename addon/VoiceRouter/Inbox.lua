-- Focus-preserving draft adapter. No addon edit box, native opening, focus or send.
local R=VoiceRouterLocal
local pending,collecting,busy,blockedAction=nil,nil,false,nil
local seen,seenOrder={},{}
local hooked=setmetatable({},{__mode="k"})
local events=CreateFrame("Frame")
local function notify(text)
    if DEFAULT_CHAT_FRAME and DEFAULT_CHAT_FRAME.AddMessage then DEFAULT_CHAT_FRAME:AddMessage("ForeverRoutedSpeech: "..text) end
end
local function focus() return R.call(GetCurrentKeyBoardFocus) end
local function restricted(edit) return edit and (R.call(edit.IsForbidden,edit) or R.call(edit.IsAnchoringRestricted,edit)) end
local function isChat(edit) return edit and (R.call(edit.GetChatType,edit) or R.call(edit.GetAttribute,edit,"chatType"))~=nil end
local function cancel() pending=nil; collecting=nil end -- never alter native focus/text
local function begin()
    cancel(); blockedAction=nil
    local edit=focus()
    if not edit or restricted(edit) or not R.call(edit.IsObjectType,edit,"EditBox") or not R.call(edit.IsShown,edit) then
        notify("Open chat with WoW's controller command, or select an empty search field, then click RS to paste."); return
    end
    collecting={edit=edit,original=R.call(edit.GetText,edit) or "",hex="",started=GetTime()}
end
local function commit()
    if not collecting then return end
    local transaction=collecting; collecting=nil
    if GetTime()-transaction.started>3 or focus()~=transaction.edit then notify("Field changed before delivery. Nothing was routed."); return end
    local control,error=R.control(transaction.hex)
    if transaction.digit~=nil then notify("Incomplete routing control digit. Nothing was routed."); return end
    if not control then notify(error); return end
    if seen[control.nonce] then notify("Repeated draft control signal. Nothing was routed."); return end
    seen[control.nonce]=true; seenOrder[#seenOrder+1]=control.nonce
    if #seenOrder>128 then seen[table.remove(seenOrder,1)]=nil end
    transaction.control=control; pending=transaction
end
local function signal(key)
    if key=="F17" then begin()
    elseif key=="F18" then commit()
    elseif key=="F19" then cancel()
    elseif collecting then
        local number=tonumber(key:match("^F(%d+)$"))
        if number and number>=13 and number<=16 and #collecting.hex<160 then
            local digit=number-13
            if collecting.digit==nil then collecting.digit=digit
            else collecting.hex=collecting.hex..string.format("%x",collecting.digit*4+digit); collecting.digit=nil end
        else cancel(); notify("Routing control signal exceeded its frame. Nothing was routed.") end
    end
end
local types={say="SAY",guild="GUILD",party="PARTY",raid="RAID",instance="INSTANCE_CHAT",general="CHANNEL",trade="CHANNEL",lookingforgroup="CHANNEL",custom="CHANNEL"}
local kinds={SAY="say",GUILD="guild",PARTY="party",RAID="raid",INSTANCE_CHAT="instance"}
local function selected(edit,context)
    local kind=R.call(edit.GetChatType,edit) or R.call(edit.GetAttribute,edit,"chatType")
    if kind=="CHANNEL" then
        local id=R.call(edit.GetChannelTarget,edit) or R.call(edit.GetAttribute,edit,"channelTarget")
        for _,c in ipairs(context.channels) do if c.id==id then return {kind=c.kind,channel=c,open=true} end end
    end
    if kind=="SAY" then return nil end -- native opening defaults to Say; current group wins ordinary speech
    return kinds[kind] and {kind=kinds[kind],open=true} or {unsupported=true,open=true}
end
local function replace(edit,expected,text)
    if focus()~=edit or restricted(edit) or R.call(edit.GetText,edit)~=expected then return nil,"Field changed. No routing edits were made." end
    busy=true; local ok=pcall(edit.SetText,edit,text); busy=false
    if not ok or R.call(edit.GetText,edit)~=text then return nil,"The field did not accept the complete draft. Review it manually." end
    return true
end
local function consume()
    if not pending or busy then return end
    local transaction=pending; pending=nil
    local edit,control=transaction.edit,transaction.control
    if focus()~=edit or not R.call(edit.IsShown,edit) or restricted(edit) then notify("Field focus changed. Nothing was routed."); return end
    local raw=R.call(edit.GetText,edit) or ""
    if transaction.original~="" then
        -- Identify our exact pasted body even when inserted into existing text.
        local matched=false
        for i=1,math.min(#raw,4096) do if R.matches(control,raw:sub(i,i+control.length-1)) then matched=true; break end end
        if matched then replace(edit,raw,transaction.original) end
        notify("Existing text preserved. Finish or clear it before another draft."); return
    end
    if not R.matches(control,raw) then notify("Paste was incomplete or the field changed. Review its text; nothing was routed or sent."); return end
    if blockedAction then notify("Draft preparation stopped: client blocked "..blockedAction.."."); return end
    if not isChat(edit) then notify("Search text ready. Confirm it with your gamepad."); return end
    local context,error=R.context()
    local decision
    if context then decision,error=R.resolve({text=raw,hint=control.hint},context,selected(edit,context)) end
    if not decision then replace(edit,raw,""); notify(error or "Requested audience unavailable. Edit or choose another destination."); return end
    local ok=pcall(function()
        if edit.SetChatType then edit:SetChatType(types[decision.kind]) else edit:SetAttribute("chatType",types[decision.kind]) end
        if decision.channel then
            if edit.SetChannelTarget then edit:SetChannelTarget(decision.channel.id) else edit:SetAttribute("channelTarget",decision.channel.id) end
        end
        if edit.UpdateHeader then edit:UpdateHeader()
        elseif type(ChatFrameUtil)=="table" and ChatFrameUtil.UpdateHeader then ChatFrameUtil.UpdateHeader(edit)
        elseif type(ChatEdit_UpdateHeader)=="function" then ChatEdit_UpdateHeader(edit) end
    end)
    local applied=R.call(edit.GetChatType,edit) or R.call(edit.GetAttribute,edit,"chatType")
    local id=R.call(edit.GetChannelTarget,edit) or R.call(edit.GetAttribute,edit,"channelTarget")
    if not ok or blockedAction or applied~=types[decision.kind] or decision.channel and id~=decision.channel.id then
        notify("Chat destination blocked or did not match. Nothing was sent; review the native header."); return
    end
    local filled,why=replace(edit,raw,decision.message)
    if not filled or blockedAction then notify(why or "Client blocked draft preparation."); return end
    local name=decision.channel and decision.channel.name.." (/"..decision.channel.id..")" or decision.kind
    notify(name.." draft ready. Press A to send.")
end
local function hook(edit)
    if not edit or restricted(edit) or hooked[edit] or type(edit.HookScript)~="function" then return end
    hooked[edit]=true
    edit:HookScript("OnTextChanged",function()
        if not busy and pending and pending.edit==edit then pending.changed=GetTime() end
    end)
    edit:HookScript("OnKeyDown",function(_,key)
        if R.call(IsControlKeyDown) and R.call(IsShiftKeyDown) and not R.call(IsAltKeyDown) and key:match("^F1[3-9]$") then signal(key); hook(focus()) end
    end)
end
local buttons={}
for i=13,19 do
    local key="F"..i
    local button=CreateFrame("Button","ForeverRoutedSpeechControl"..i,UIParent)
    button:SetScript("OnClick",function() signal(key); hook(focus()) end)
    buttons[i]=button
end
local function bind()
    if R.call(InCombatLockdown) then return end
    if type(SetOverrideBindingClick)~="function" then notify("Routing shortcut API unavailable. Plain speech remains on the clipboard."); return end
    for i=13,19 do SetOverrideBindingClick(buttons[i],true,"CTRL-SHIFT-F"..i,"ForeverRoutedSpeechControl"..i) end
end
events:RegisterEvent("PLAYER_LOGIN"); events:RegisterEvent("PLAYER_REGEN_ENABLED")
events:RegisterEvent("ADDON_ACTION_BLOCKED"); events:RegisterEvent("ADDON_ACTION_FORBIDDEN")
events:SetScript("OnEvent",function(_,event,addonName,functionName)
    if event=="ADDON_ACTION_BLOCKED" or event=="ADDON_ACTION_FORBIDDEN" then
        if addonName=="VoiceRouter" or addonName=="ForeverRoutedSpeech" then
            blockedAction=tostring(functionName); cancel()
            notify("Client blocked "..blockedAction.." ("..event.."). No send or retry.")
        end
        return
    end
    bind(); hook(focus())
end)
events:SetScript("OnUpdate",function()
    hook(focus())
    if collecting and GetTime()-collecting.started>3 then cancel() end
    if pending and GetTime()-pending.started>3 then cancel(); notify("Paste adapter timed out. Nothing was sent.")
    elseif pending and pending.changed and GetTime()-pending.changed>=.1 then consume() end
end)
-- No addon slash handlers: Forever's native cleanup after addon commands can taint gamepad focus.
