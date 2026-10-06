-- Context and final destination selection stay inside WoW. No outgoing telemetry.
VoiceRouterLocal = {}
local R = VoiceRouterLocal
function R.call(fn, ...)
    if type(fn) ~= "function" then return nil end
    local ok, value = pcall(fn, ...)
    if ok then return value end
end
function R.checksum(text)
    local a,b=1,0
    for i=1,#text do a=(a+text:byte(i))%65521; b=(b+a)%65521 end
    return b*65536+a
end
function R.validText(text)
    return type(text)=="string" and #text>0 and #text<=200 and not text:find("[%z\1-\31\127]") and not text:match("^%s*/") and text:find("%S")~=nil
end
function R.decode(packet)
    if type(packet)~="string" or #packet>512 then return nil,"Invalid draft packet." end
    local nonce,hint,send,length,hash,text=packet:match("^/frs1 ([a-f0-9]+) ([a-z0-9:]+) ([01]) (%d+) ([a-f0-9]+) (.*)$")
    if not nonce or #nonce~=32 or #hash~=8 or not R.validText(text) or #text~=tonumber(length) or
        R.checksum("/frs1 "..nonce.." "..hint.." "..send.." "..length.." "..text)~=tonumber(hash,16) then
        return nil,"Incomplete or damaged draft. Nothing was sent."
    end
    local audience=hint:match("^[im]:(.+)$")
    local valid={say=true,guild=true,party=true,raid=true,instance=true,general=true,trade=true,lookingforgroup=true}
    if hint~="default" and not valid[audience] and not hint:match("^m:channel:[1-9]%d?%d?%d?$") then return nil,"Invalid audience hint." end
    return {nonce=nonce,hint=hint,send=send=="1",text=text}
end
-- v2 metadata is carried only by bound function keys, never in a native text field.
function R.control(hex)
    if type(hex)~="string" or #hex==0 or #hex>160 or #hex%2~=0 or hex:find("[^a-f0-9]") then return nil,"Damaged routing control signal." end
    local raw=hex:gsub("..",function(pair) return string.char(tonumber(pair,16)) end)
    local nonce,hint,length,hash=raw:match("^frs2 ([a-f0-9]+) ([a-z0-9:]+) (%d+) ([a-f0-9]+)$")
    if not nonce or #nonce~=32 or #hash~=8 or tonumber(length)<1 or tonumber(length)>200 then return nil,"Invalid routing control metadata." end
    local audience=hint:match("^[im]:(.+)$")
    local valid={say=true,guild=true,party=true,raid=true,instance=true,general=true,trade=true,lookingforgroup=true}
    if hint~="default" and not valid[audience] and not hint:match("^m:channel:[1-9]%d?%d?%d?$") then return nil,"Invalid audience hint." end
    return {nonce=nonce,hint=hint,length=tonumber(length),hash=tonumber(hash,16),header="frs2 "..nonce.." "..hint.." "..length}
end
function R.matches(control,text)
    return R.validText(text) and #text==control.length and R.checksum(control.header.." "..text)==control.hash
end
function R.context()
    local guild,party,raid=R.call(IsInGuild),R.call(IsInGroup),R.call(IsInRaid)
    if type(guild)~="boolean" or type(party)~="boolean" or type(raid)~="boolean" then return nil,"Group/guild APIs unavailable." end
    if type(GetChannelList)~="function" then return nil,"Joined-channel API unavailable." end
    local values={pcall(GetChannelList)}
    if not values[1] or (#values-1)%3~=0 then return nil,"Joined-channel API returned an unsupported shape." end
    local channels={}
    for i=2,#values,3 do
        local id,name,disabled=values[i],values[i+1],values[i+2]
        if type(id)~="number" or type(name)~="string" or type(disabled)~="boolean" then return nil,"Invalid joined-channel data." end
        if id>0 and not disabled then
            local lower=name:lower()
            local kind=(lower=="general" or lower:match("^general%s*%-")) and "general" or
                (lower=="trade" or lower:match("^trade%s*%-")) and "trade" or
                (lower=="lookingforgroup" or lower=="looking for group") and "lookingforgroup" or "custom"
            channels[#channels+1]={id=id,name=name,kind=kind}
        end
    end
    local zone=R.call(GetZoneText) or ""
    local capitals={["Stormwind City"]=true,["Ironforge"]=true,["Orgrimmar"]=true,["Darnassus"]=true,["Undercity"]=true,["Thunder Bluff"]=true}
    local city=capitals[zone]
    if not city and type(C_Map)=="table" then
        local map=R.call(C_Map.GetBestMapForUnit,"player")
        local info=map and R.call(C_Map.GetMapInfo,map)
        local flag=type(Enum)=="table" and Enum.UIMapFlag and Enum.UIMapFlag.IsCityMap
        if info and type(info.flags)=="number" and type(flag)=="number" and flag>0 then city=math.floor(info.flags/flag)%2==1 end
    end
    return {guild=guild,party=party,raid=raid,instance=LE_PARTY_CATEGORY_INSTANCE and R.call(IsInGroup,LE_PARTY_CATEGORY_INSTANCE)==true,
        channels=channels,zone=zone,subzone=R.call(GetSubZoneText) or "",city=city,resting=R.call(IsResting)==true}
end
local names={{"everyone around me","say"},{"everyone nearby","say"},{"looking for group","lookingforgroup"},
    {"instance","instance"},{"general","general"},{"guild","guild"},{"party","party"},{"raid","raid"},{"trade","trade"},{"lfg","lookingforgroup"},{"say","say"}}
local verbs={"tell ","say to ","say in ","ask in ","speak to ","send to ","send in ","post in ","in ","to "}
local function trim(text) return text:match("^%s*(.-)%s*$") end
function R.explicit(text,context)
    local choices={}
    for _,pair in ipairs(names) do choices[#choices+1]={name=pair[1],kind=pair[2]} end
    for _,channel in ipairs(context.channels) do
        if channel.kind=="custom" then choices[#choices+1]={name=channel.name:lower(),kind="custom",channel=channel} end
    end
    table.sort(choices,function(a,b) return #a.name>#b.name end)
    local lower=text:lower()
    for _,choice in ipairs(choices) do
        for _,verb in ipairs(verbs) do
            for _,article in ipairs({"","the "}) do
                local prefix=verb..article..choice.name
                if lower:sub(1,#prefix)==prefix then
                    local tail=text:sub(#prefix+1)
                    if tail:lower():sub(1,5)==" chat" then tail=tail:sub(6) end
                    if tail=="" or tail:match("^[%s,%.:]") then
                        tail=trim(tail:gsub("^[%s,%.:]+",""))
                        if tail:lower():sub(1,5)=="that " then tail=tail:sub(6) end
                        return choice.kind,choice.channel,tail
                    end
                end
            end
        end
        local prefix=choice.name
        if lower:sub(1,#prefix)==prefix then
            local tail=text:sub(#prefix+1)
            if tail:lower():sub(1,5)==" chat" then tail=tail:sub(6) end
            if tail:match("^[,%.:]") then return choice.kind,choice.channel,trim(tail:sub(2)) end
        end
    end
    -- Explicit unknown named channels must never silently become Say/group chat.
    if lower:match("^ask in ") or lower:match("^send to ") or lower:match("^speak to ") or
        lower:match("^in .-chat[%s,%.:]") or lower:match("^in .-[,:]") then return "unavailable",nil,text end
end
function R.available(kind,channel,context)
    if kind=="say" then return true end
    if kind=="guild" then return context.guild end
    if kind=="party" then return context.party end
    if kind=="raid" then return context.raid end
    if kind=="instance" then return context.instance end
    if channel then
        for _,joined in ipairs(context.channels) do if joined.id==channel.id and joined.name==channel.name then return true end end
    end
    return false
end
function R.resolve(packet,context,selected)
    local kind,channel,message=R.explicit(packet.text,context)
    if packet.hint:match("^m:") then kind=nil; channel=nil end
    local reason="explicit instruction"
    if not kind then
        message=message or packet.text
        local manual=packet.hint:match("^m:(.+)$")
        kind=manual or packet.hint:match("^i:(.+)$")
        if kind and kind:match("^channel:") then
            local id=tonumber(kind:sub(9)); kind="custom"
            for _,c in ipairs(context.channels) do if c.id==id then channel=c end end
        elseif kind then
            for _,c in ipairs(context.channels) do if c.kind==kind then channel=c; break end end
        end
        reason=manual and "manual destination" or "model suggestion"
        if kind and not manual and not R.available(kind,channel,context) then kind=nil end
    else
        if not channel then for _,c in ipairs(context.channels) do if c.kind==kind then channel=c; break end end end
    end
    if not kind then
        if selected and selected.open and selected.unsupported then return nil,"Unsupported open chat audience. Choose a supported destination." end
        if selected and selected.open and R.available(selected.kind,selected.channel,context) then kind=selected.kind; channel=selected.channel
        elseif context.instance then kind="instance" elseif context.raid then kind="raid" elseif context.party then kind="party"
        elseif selected and R.available(selected.kind,selected.channel,context) then kind=selected.kind; channel=selected.channel
        else kind="say" end
        reason="current group / selected chat / Say default"
    end
    if not R.available(kind,channel,context) then return nil,"Requested destination is unavailable. Draft retained in the companion." end
    if not R.validText(message) then return nil,"Empty or invalid message after routing. Nothing was sent." end
    return {kind=kind,channel=channel,message=message,reason=reason,zone=context.zone}
end
