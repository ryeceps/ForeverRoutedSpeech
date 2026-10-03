-- Read-only preview of text already entered through the game's input controls.
-- No clipboard access, text replacement, SendChatMessage or external input.
local panel = CreateFrame("Frame", "VoiceRouterDraftPreview", UIParent)
panel:SetSize(460, 190)
panel:SetPoint("BOTTOM", UIParent, "BOTTOM", 0, 180)
panel:SetFrameStrata("DIALOG")
panel:EnableMouse(false)
local background = panel:CreateTexture(nil, "BACKGROUND")
background:SetAllPoints(panel)
background:SetColorTexture(.04, .07, .1, .94)
local heading = panel:CreateFontString(nil, "OVERLAY", "GameFontNormal")
heading:SetPoint("TOPLEFT", 12, -12)
heading:SetWidth(436)
heading:SetJustifyH("LEFT")
local message = panel:CreateFontString(nil, "OVERLAY", "GameFontHighlight")
message:SetPoint("TOPLEFT", 12, -40)
message:SetWidth(436)
message:SetJustifyH("LEFT")
message:SetWordWrap(true)
panel:Hide()

local function call(fn, ...)
    if type(fn) ~= "function" then return nil end
    local ok, value = pcall(fn, ...)
    if ok then return value end
end
local function plain(value)
    if type(issecretvalue) == "function" and issecretvalue(value) then return false end
    return type(value) == "string"
end
local function activeChat()
    if type(ChatFrameUtil) == "table" and type(ChatFrameUtil.GetActiveWindow) == "function" then
        return call(ChatFrameUtil.GetActiveWindow)
    end
    return call(ChatEdit_GetActiveWindow)
end
local function target()
    local edit = activeChat()
    if edit then return edit, "Chat" end
    local focus = call(GetCurrentKeyBoardFocus)
    if not focus then return nil end
    local name = call(focus.GetName, focus)
    local db = VoiceRouterProbeDB
    if plain(name) and db and db.textFields and db.textFields[name] then
        return focus, "Auction House search"
    end
end
local function clear()
    heading:SetText("")
    message:SetText("")
    panel:Hide()
end
local function refresh()
    local edit, kind = target()
    if not edit then clear(); return end
    local text = call(edit.GetText, edit)
    if not plain(text) then clear(); return end
    if kind == "Chat" then
        local channel = call(edit.GetChatType, edit) or call(edit.GetAttribute, edit, "chatType")
        if plain(channel) then kind = "Chat: " .. channel end
    end
    heading:SetText(kind .. " — review, then use your gamepad confirm")
    -- Escape UI formatting while preserving the underlying field unchanged.
    message:SetText(text == "" and "Waiting for your controller paste…" or text:gsub("|", "||"))
    panel:Show()
end
local elapsed = 0
-- The updater stays visible: a hidden frame cannot receive OnUpdate.
local updater = CreateFrame("Frame")
updater:SetScript("OnUpdate", function(_, dt)
    elapsed = elapsed + dt
    if elapsed >= .25 then elapsed = elapsed % .25; refresh() end
end)

BINDING_HEADER_VOICEROUTER = "Voice Router"
BINDING_NAME_VOICEROUTER_OPEN = "Open chat / show draft preview"
function VoiceRouter_OpenDraft()
    -- Preserve existing focused chat and registered search fields.
    if target() then refresh(); return end
    if type(ChatFrameUtil) == "table" and type(ChatFrameUtil.OpenChat) == "function" then
        call(ChatFrameUtil.OpenChat, "")
    else
        call(ChatEdit_OpenChat, "")
    end
    refresh()
end
