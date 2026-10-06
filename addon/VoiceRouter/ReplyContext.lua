-- Ephemeral conversation cues, read from chat events. No SavedVariables or outgoing telemetry.
local R=VoiceRouterLocal
local recent={}
local ttl,shortWindow,maxEntries=45,25,32
local kinds={CHAT_MSG_SAY="say",CHAT_MSG_GUILD="guild",CHAT_MSG_PARTY="party",CHAT_MSG_PARTY_LEADER="party",
    CHAT_MSG_RAID="raid",CHAT_MSG_RAID_LEADER="raid",CHAT_MSG_INSTANCE_CHAT="instance",CHAT_MSG_INSTANCE_CHAT_LEADER="instance"}
local stop={the=true,["and"]=true,that=true,this=true,with=true,from=true,have=true,just=true,what=true,your=true,you=true,
    ["for"]=true,are=true,was=true,were=true,its=true,["not"]=true,but=true,can=true,all=true,any=true,anyone=true,
    guys=true,dude=true,yeah=true,yes=true,thanks=true,thank=true,hello=true,cool=true,super=true,looks=true}
local function secret(value) return type(issecretvalue)=="function" and issecretvalue(value) end
local function words(text)
    text=text:gsub("|c%x%x%x%x%x%x%x%x",""):gsub("|r",""):gsub("|H.-|h(.-)|h","%1"):lower()
    local result={}
    for word in text:gmatch("[%a%d]+") do if #word>=4 and not stop[word] then result[word]=true end end
    return result
end
local function prune()
    local now=GetTime()
    for i=#recent,1,-1 do if now-recent[i].time>ttl or now<recent[i].time then table.remove(recent,i) end end
end
local function observe(event,text,author,language,fullChannel,unused,flags,zoneChannel,channelIndex,channelBase,languageId,lineId,guid)
    if secret(text) or secret(author) or secret(guid) or secret(channelIndex) or secret(channelBase) then return end
    if type(text)~="string" or type(author)~="string" or #text>1024 or #author>128 then return end
    local ownGuid,ownName=R.call(UnitGUID,"player"),R.call(UnitName,"player")
    if secret(ownGuid) or secret(ownName) or not ownGuid and not ownName then return end
    author=author:match("^[^-]+") or author
    if guid and guid==ownGuid or ownName and author:lower()==ownName:lower() then return end
    local context=R.context()
    if not context then return end
    local kind,channel=kinds[event],nil
    if event=="CHAT_MSG_CHANNEL" then
        if type(channelIndex)~="number" or type(channelBase)~="string" then return end
        local base=channelBase:lower():gsub("%s*%-.*$",""):gsub("%s","")
        for _,joined in ipairs(context.channels) do
            if joined.id==channelIndex and joined.name:lower():gsub("%s*%-.*$",""):gsub("%s","")==base then
                kind=joined.kind; channel=joined; break
            end
        end
        -- Custom-channel intent remains explicit/manual only.
        if not kind or kind=="custom" then return end
    end
    if not kind or not R.available(kind,channel,context) then return end
    prune()
    recent[#recent+1]={kind=kind,channelName=channel and channel.name,names=words(author),terms=words(text),time=GetTime()}
    if #recent>maxEntries then table.remove(recent,1) end
end
local function destination(entry,context)
    local channel
    if entry.channelName then
        for _,joined in ipairs(context.channels) do if joined.name==entry.channelName then channel=joined; break end end
    end
    if R.available(entry.kind,channel,context) then return entry.kind,channel end
end
local function same(a,b) return a.kind==b.kind and a.channelName==b.channelName end
function R.reply(text,context)
    prune()
    local terms=words(text)
    local lower=text:lower():gsub("^%s+","")
    local first=lower:match("^([%a%d]+)")
    local cue=lower:match("^yeah[%s,%.!]") or lower:match("^yes[%s,%.!]") or lower:match("^no[%s,%.!]") or
        lower:match("^thanks") or lower:match("^thank you") or lower:match("^wow[%s,%.!]") or
        lower:match("^agreed") or lower:match("^nice[%s,%.!]") or lower:match("^that's ") or lower:match("^that is ") or
        lower:match("^you ") or lower:match("^your ") or lower=="yes" or lower=="yeah" or lower=="no" or lower=="nice"
    local best,bestScore,ambiguous=nil,0,false
    local short,shortAmbiguous=nil,false
    for _,entry in ipairs(recent) do
        local kind,channel=destination(entry,context)
        if kind then
            local name=false
            for word in pairs(entry.names) do if terms[word] then name=true; break end end
            local overlap=0
            for word in pairs(terms) do if entry.terms[word] then overlap=overlap+1 end end
            local score=name and (cue or entry.names[first]) and 3 or cue and overlap>=2 and 2 or 0
            if score>bestScore then best=entry;bestScore=score;ambiguous=false
            elseif score>0 and score==bestScore and not same(entry,best) then ambiguous=true end
            if GetTime()-entry.time<=shortWindow then
                if not short then short=entry elseif not same(short,entry) then shortAmbiguous=true end
            end
        end
    end
    if best and not ambiguous then
        local kind,channel=destination(best,context)
        return kind,channel,"recent chat reply ("..(bestScore==3 and "speaker name" or "topic overlap")..")"
    end
    -- A bare reply only follows one recent audience, and never redirects group conversation elsewhere.
    if bestScore==0 and cue and short and not shortAmbiguous and not (context.party or context.raid or context.instance) then
        local kind,channel=destination(short,context)
        return kind,channel,"recent chat reply (single active conversation)"
    end
end
local events=CreateFrame("Frame","ForeverRoutedSpeechReplyContext")
for event in pairs(kinds) do pcall(events.RegisterEvent,events,event) end
pcall(events.RegisterEvent,events,"CHAT_MSG_CHANNEL")
events:RegisterEvent("PLAYER_ENTERING_WORLD")
events:SetScript("OnEvent",function(_,event,...)
    if event=="PLAYER_ENTERING_WORLD" then recent={}; return end
    -- Restricted/secret client payloads are skipped, never inspected through another mechanism.
    pcall(observe,event,...)
end)
events:SetScript("OnUpdate",function() prune() end)
