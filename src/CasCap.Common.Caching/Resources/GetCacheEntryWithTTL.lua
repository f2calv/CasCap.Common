local cacheKey      =   @cacheKey			--the key of the cacheEntry
local cacheExpiry   =   @cacheExpiry		--the expiry time of the cacheEntry (optional)
local trackKey      =   @trackKey			--the key of a tracking entry (non-essential)
local trackCaller   =   @trackCaller		--the name of the calling method

redis.call('HINCRBY', trackKey, trackCaller, 1)
redis.call('EXPIRE', trackKey, 604800)

--get the ttl on the item
local _ttl = redis.call('TTL', cacheKey)
local _type
local _payload

--if ttl comes back as > -2 then the item exists
if _ttl > -2 then
	--check the data type of the cached item, we can only use this script to return string values
    _type = redis.call('TYPE', cacheKey)['ok']
    if _type == 'string' then
        if cacheExpiry == '-1' then
            _payload = redis.call('GET', cacheKey)
        else
            _payload = redis.call("GETEX", cacheKey, "EX", cacheExpiry)
            _ttl = cacheExpiry
        end
    else
        _payload = 'non-string'
    end
end

return {_ttl, _type, _payload}
