-- No SendChatMessage, input simulation, or live SavedVariables transport.
local addon = CreateFrame("Frame", "VoiceRouterStatusStrip", UIParent)
local columns, rows, cell = 128, 32, 1
-- Keep cells at one physical pixel even when the game UI is scaled.
if addon.SetScale and UIParent.GetEffectiveScale then addon:SetScale(1 / UIParent:GetEffectiveScale()) end
addon:SetSize(columns * cell, rows * cell)
addon:SetPoint("TOPLEFT", UIParent, "TOPLEFT", 16, -64)
addon:SetFrameStrata("TOOLTIP")
addon:EnableMouse(true)
addon:SetMovable(true)
addon:RegisterForDrag("LeftButton")
addon:SetScript("OnDragStart", function(self) self:StartMoving() end)
addon:SetClampedToScreen(true)
addon:SetScript("OnDragStop", function(self)
    self:StopMovingOrSizing()
    local point, _, relativePoint, x, y = self:GetPoint()
    VoiceRouterStripDB = VoiceRouterStripDB or {}
    VoiceRouterStripDB.point, VoiceRouterStripDB.relativePoint, VoiceRouterStripDB.x, VoiceRouterStripDB.y = point, relativePoint, x, y
    print("Voice Router: strip position saved. Update capture.json after moving the strip.")
end)
local pixels, lastPixels = {}, {}
for y = 0, rows - 1 do
    for x = 0, columns - 1 do
        local t = addon:CreateTexture(nil, "OVERLAY")
        t:SetSize(cell, cell)
        t:SetPoint("TOPLEFT", addon, "TOPLEFT", x * cell, -y * cell)
        t:SetColorTexture(0, 0, 0, 1)
        pixels[#pixels + 1] = t
        lastPixels[#pixels] = 0
    end
end
local label = addon:CreateFontString(nil, "OVERLAY", "GameFontNormalSmall")
label:SetPoint("BOTTOMLEFT", addon, "TOPLEFT", 0, 2)
label:SetText("Voice Router | drag to move")
label:Hide()
addon:SetScript("OnEnter", function() label:Show() end)
addon:SetScript("OnLeave", function() label:Hide() end)
local session = math.floor((GetTime() * 1000 + math.random(1, 1000000)) % 4294967296)
local seq, elapsed = 0, 0
local function safe(fn, ...)
    if type(fn) ~= "function" then return nil end
    local ok, value = pcall(fn, ...)
    if ok then return value end
    return nil
end
local function pack32(v)
    local bytes = {}
    for i = 1, 4 do bytes[i] = string.char(v % 256); v = math.floor(v / 256) end
    return table.concat(bytes)
end
local function checksum(text)
    local a, b = 1, 0
    for i = 1, #text do a = (a + string.byte(text, i)) % 65521; b = (b + a) % 65521 end
    return b * 65536 + a
end
local function escape(text)
    return (text:gsub("([^A-Za-z0-9 %-%_])", function(c) return string.format("%%%02X", string.byte(c)) end))
end
local function build()
    if type(GetBuildInfo) ~= "function" then return "unknown", 0 end
    local ok, version, number, _, interface = pcall(GetBuildInfo)
    if not ok then return "unknown", 0 end
    return tostring(number or version or "unknown"), tonumber(interface) or 0
end
local function channels()
    if type(GetChannelList) ~= "function" then return nil end
    local results = {pcall(GetChannelList)}
    if not results[1] then return nil end
    local list = {}
    -- Candidate triplet shape must be observed on Forever. Reject other shapes.
    if (#results - 1) % 3 ~= 0 then return nil end
    for i = 2, #results, 3 do
        local id, name, disabled = results[i], results[i+1], results[i+2]
        if type(id) ~= "number" or type(name) ~= "string" or type(disabled) ~= "boolean" then return nil end
        if not disabled and id > 0 then
            local lower = name:lower()
            local kind = (lower == "general" or lower:match("^general%s*%-")) and "General" or
                (lower == "trade" or lower:match("^trade%s*%-")) and "Trade" or
                (lower == "lookingforgroup" or lower == "looking for group") and "LookingForGroup" or "Custom"
            list[#list+1] = tostring(id) .. "," .. kind .. "," .. escape(name)
        end
    end
    return table.concat(list, ";")
end
local candidate = {Say="/say", Guild="/g", Party="/p", Raid="/raid", Instance="/i", Custom="numbered"}
local globals = {Say="SAY", Guild="GUILD", Party="PARTY", Raid="RAID", Instance="INSTANCE_CHAT"}
local function prefixExists(kind)
    if kind == "Custom" then return channels() ~= nil end
    local key = globals[kind]
    for i = 1, 20 do if _G["SLASH_" .. key .. i] == candidate[kind] then return true end end
    return false
end
local function chatApi()
    if type(ChatFrameUtil) == "table" and type(ChatFrameUtil.GetActiveWindow) == "function" then return ChatFrameUtil.GetActiveWindow end
    if type(ChatEdit_GetActiveWindow) == "function" then return ChatEdit_GetActiveWindow end
end
local lastField
local function focusedField(db)
    if type(GetCurrentKeyBoardFocus) ~= "function" then return "none", "", 0, "chars" end
    local focus = safe(GetCurrentKeyBoardFocus)
    if not focus then return "none", "", 0, "chars" end
    local active = safe(chatApi())
    if active == focus then return "none", "", 0, "chars" end
    local name = safe(focus.GetName, focus)
    if type(name) ~= "string" or name == "" then name = "unnamed" end
    lastField = name
    local verified = db and db.textFields and db.textFields[name]
    if verified and db.build == select(1, build()) then return verified.kind or "auctionhouse", name, verified.limit, verified.units end
    return "unsupported", name, 0, "chars"
end
local function activeAudience(joined, chatInput)
    if chatInput ~= "open" then return "none", "" end
    local active = safe(chatApi())
    if not active then return "none", "" end
    local kind = safe(active.GetAttribute, active, "chatType")
    if not kind then return "none", "" end
    local names = {SAY="Say", GUILD="Guild", PARTY="Party", RAID="Raid", INSTANCE_CHAT="Instance"}
    if names[kind] then return names[kind], "" end
    if kind == "CHANNEL" then
        local id = tonumber(safe(active.GetAttribute, active, "channelTarget"))
        if id then
            for record in joined:gmatch("[^;]+") do
                local number, audience = record:match("^(%d+),([^,]+),")
                if tonumber(number) == id then return audience, tostring(id) end
            end
        end
    end
    return "unsupported", ""
end
local function emit()
    local number = build()
    local db = VoiceRouterProbeDB
    local group, guild = "solo", safe(IsInGuild)
    local grouped, raid = safe(IsInGroup), safe(IsInRaid)
    if type(guild) ~= "boolean" or type(grouped) ~= "boolean" or type(raid) ~= "boolean" then
        -- Freeze heartbeat; missing APIs never fabricate solo context.
        label:SetText("Voice Router: context APIs missing; manual confirmation required")
        return
    end
    local instance = LE_PARTY_CATEGORY_INSTANCE and safe(IsInGroup, LE_PARTY_CATEGORY_INSTANCE)
    if instance == true then group = "instance" elseif raid then group = "raid" elseif grouped then group = "party" end
    local joined = channels()
    if joined == nil then label:SetText("Voice Router: channel API shape unverified"); return end
    local verified = {}
    if db and db.build == number and db.rendered then
        for kind, value in pairs(candidate) do
            if db.destinations[kind] and prefixExists(kind) then verified[#verified+1] = kind .. "=" .. value end
        end
    end
    table.sort(verified)
    local limit = db and db.build == number and db.limit or 0
    local units = db and db.units or "bytes"
    local chatInput = "unknown"
    if chatApi() and type(GetCurrentKeyBoardFocus) == "function" then
        local ok, active = pcall(chatApi())
        local focusOK, focus = pcall(GetCurrentKeyBoardFocus)
        if ok and focusOK then chatInput = active and active == focus and "open" or "closed" end
    elseif db and db.build == number and db.chatInputVerified and chatApi() then
        local ok, active = pcall(chatApi())
        if ok then chatInput = active and "open" or "closed" end
    end
    local fieldKind, fieldName, fieldLimit, fieldUnits = focusedField(db)
    local audience, channelId = activeAudience(joined, chatInput)
    local inputBytes, inputChecksum = "-1", ""
    local focus = safe(GetCurrentKeyBoardFocus)
    if focus and (chatInput == "open" or fieldKind == "auctionhouse" or fieldKind == "search") then
        local text = safe(focus.GetText, focus)
        if type(text) == "string" and not (type(issecretvalue) == "function" and safe(issecretvalue, text)) then
            inputBytes, inputChecksum = tostring(#text), tostring(checksum(text))
        end
    end
    local payload = table.concat({"4", number, group, guild and "1" or "0", tostring(limit), units, table.concat(verified, ";"), joined, chatInput, fieldKind, escape(fieldName), tostring(fieldLimit), fieldUnits, audience, channelId, inputBytes, inputChecksum}, "\t")
    if #payload > 494 then label:SetText("Voice Router: context exceeds strip capacity"); return end
    seq = (seq + 1) % 4294967296
    local data = "WVR1" .. string.char(#payload % 256, math.floor(#payload / 256)) .. pack32(session) .. pack32(seq) .. payload
    data = data .. pack32(checksum(data))
    for i, pixel in ipairs(pixels) do
        local value = string.byte(data, math.floor((i-1) / 8) + 1) or 0
        local white = math.floor(value / (2 ^ ((i-1) % 8))) % 2
        if lastPixels[i] ~= white then
            pixel:SetColorTexture(white, white, white, 1)
            lastPixels[i] = white
        end
    end
    label:SetText("Voice Router: " .. number .. " | " .. group .. " | /wvr report")
end
addon:SetScript("OnUpdate", function(_, dt)
    elapsed = elapsed + dt
    if elapsed >= .25 then elapsed = elapsed % .25; emit() end
end)
addon:RegisterEvent("ADDON_LOADED")
addon:SetScript("OnEvent", function(_, _, name)
    if name ~= "VoiceRouter" then return end
    if VoiceRouterStripDB then
        local p = VoiceRouterStripDB
        if type(p.point) == "string" and type(p.relativePoint) == "string" and type(p.x) == "number" and type(p.y) == "number" then
            addon:ClearAllPoints()
            addon:SetPoint(p.point, UIParent, p.relativePoint, p.x, p.y)
        end
    end
    local number = build()
    if not VoiceRouterProbeDB or VoiceRouterProbeDB.build ~= number then
        VoiceRouterProbeDB = {build=number, destinations={}, limit=0, units="bytes", rendered=false}
    end
end)
SLASH_VOICEROUTER1 = "/wvr"
SlashCmdList.VOICEROUTER = function(text)
    local command, arg, extra = text:match("^(%S*)%s*(%S*)%s*(%S*)")
    local db = VoiceRouterProbeDB
    if not db then print("Voice Router: wait for addon initialization"); return end
    if command == "rendered" then db.rendered = true; print("Strip visibility confirmed for this build.")
    elseif command == "chatinput" then
        if chatApi() then db.chatInputVerified = true; print("Recorded manual open/closed chat input verification.")
        else print("Chat input API missing; autosend remains unavailable.") end
    elseif command == "verify" then
        local kind
        for key in pairs(candidate) do if key:lower() == arg:lower() then kind = key end end
        if kind and prefixExists(kind) then db.destinations[kind] = true; print("Confirmed manually tested prefix: " .. candidate[kind])
        else print("Destination unsupported or prefix not observed; remains disabled.") end
    elseif command == "field" then
        local n = tonumber(extra)
        if lastField and lastField ~= "unnamed" and (arg == "bytes" or arg == "chars") and n and n > 0 and n <= 4096 and n == math.floor(n) then
            db.textFields = db.textFields or {}
            db.textFields[lastField] = {limit=n, units=arg, kind=lastField:lower():find("auction", 1, true) and "auctionhouse" or "search"}
            print("Confirmed previously focused search field: " .. lastField)
        else print("Focus a named search box first, verify its limit, then /wvr field bytes|chars <limit>") end
    elseif command == "limit" then
        local n = tonumber(extra)
        if (arg == "bytes" or arg == "chars") and n and n > 0 and n <= 4096 and n == math.floor(n) then
            db.limit, db.units = n, arg; print("Recorded manually verified message limit.")
        else print("Usage: /wvr limit bytes|chars <verified limit>") end
    elseif command == "reset" then
        db.destinations, db.limit, db.rendered, db.chatInputVerified, db.textFields = {}, 0, false, false, {}
        print("All compatibility confirmations reset.")
    elseif command == "preview" then
        VoiceRouterStripDB = VoiceRouterStripDB or {}
        VoiceRouterStripDB.previewEnabled = arg == "on"
        print("Voice Router: large preview " .. (VoiceRouterStripDB.previewEnabled and "enabled" or "hidden") .. ".")
    elseif command == "hide" then addon:Hide()
    elseif command == "show" then addon:Show()
    else
        local number, interface = build()
        print("Voice Router probe: build " .. number .. ", interface " .. interface)
        print("API booleans: guild=" .. tostring(safe(IsInGuild)) .. ", group=" .. tostring(safe(IsInGroup)) .. ", raid=" .. tostring(safe(IsInRaid)))
        print("Channel triplets: " .. tostring(channels()))
        for kind, prefix in pairs(candidate) do print(kind .. ": " .. prefix .. " observed=" .. tostring(prefixExists(kind)) .. " confirmed=" .. tostring(db.destinations[kind] or false)) end
        print("Limit=" .. db.limit .. " " .. db.units .. "; visible=" .. tostring(db.rendered))
        print("Autosend chat input manually verified=" .. tostring(db.chatInputVerified or false))
        print("Previously observed non-chat focus=" .. tostring(lastField) .. "; verify only a tested Auction House search field with /wvr field bytes|chars <n>")
        print("After actual client tests: /wvr rendered; /wvr verify <say|guild|party|raid|instance|custom>; /wvr limit bytes|chars <n>")
    end
end
