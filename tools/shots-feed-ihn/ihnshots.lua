-- IHN live shots feed for the NHL 24 score clock (HMS).
-- Installed over the unused debug file /features/debug/vvms/widgettester/widgettester.lua and loaded as the lua VM of
-- /features/overlays/vvms/scoreclock/scoreclock.vvm. Polls the game-stats team-performance service (the pause menu's
-- shot totals) and publishes "away|home" on bnd_ihn_shots; the score clock layout binds it to a hidden label that the
-- IHN movie reads. A text starting with "!" is a status for the first in-game test.

local IhnShots = {}

local BND = "bnd_ihn_shots"
local URN = "nhl:game-stats:team-performance:info:get:01.00.00"
local MED_FREQUENCY = 1

function IhnShots:new(init)
    local o = init or {}
    setmetatable(o, self)
    self.__index = self
    o.mWatch = nil
    o.mLast = ""
    o.mBound = false
    local ok, err = pcall(function() o:_Start() end)
    if not ok then
        o:_Say("!start " .. tostring(err))
    end
    return o
end

-- every text carries the tag IHNSOG: the score clock movie finds the label's TextField by it
function IhnShots:_Say(text)
    if self.mBound and text ~= self.mLast then
        self.mLast = text
        pcall(function() self.im.Publish(BND, "IHNSOG" .. text) end)
    end
end

function IhnShots:_Start()
    self.im.LoadDataBinding({ viewname = "ihn_shots", binding = BND, type = "string" })
    self.mBound = true
    self:_Say("!init")
    if self.api == nil then
        self:_Say("!no api")
        return
    end
    self.mDataWatch = self.api("DataWatch")
    if self.mDataWatch == nil then
        self:_Say("!no datawatch")
        return
    end
    local input =
    {
        frequency = MED_FREQUENCY,
        data =
        {
            RequestMode = "Sequential",
            ResponseFormat = "MapByUniqueId",
            RequestList =
            {
                {
                    URN = URN,
                    Input = { noinputs = {} },
                    ID = "ihn_tp"
                }
            }
        }
    }
    self.mWatch = self.mDataWatch.SetupWatch(input, function(success, output) self:_OnData(success, output) end)
    self:_Say("!watching")
end

function IhnShots:_OnData(success, output)
    if not success then
        self:_Say("!no data")
        return
    end
    local ok, err = pcall(function()
        local all = output.ResponseAsMapByUniqueId.ihn_tp.Output.PeriodStatsList[1]
        self:_Say(tostring(all.AwayTeamStats.Shots) .. "|" .. tostring(all.HomeTeamStats.Shots))
    end)
    if not ok then
        self:_Say("!bad data " .. tostring(err))
    end
end

function IhnShots:finalize()
    pcall(function()
        if self.mWatch ~= nil and self.mDataWatch ~= nil then
            self.mDataWatch.RemoveWatch(self.mWatch)
        end
    end)
    self.mWatch = nil
end

return IhnShots
