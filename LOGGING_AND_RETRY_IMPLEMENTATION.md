# SC2Pulse Request Logging & Retry Policy Implementation

## What Was Implemented

### 1. Request-Level Logging in Sc2PulseClient

**File**: `src/sc2pulse/Sc2PulseClient.cs`

- Added `SetDebugLogger(Action<string>? logCallback)` method to configure logging
- Implemented `ExecuteWithRetryAsync()` helper method that wraps all HTTP requests
- All API calls now log:
  - Request URL with full query parameters
  - Response status code and latency (in milliseconds)
  - Retry attempts with exponential backoff delays
  - Timeout and network error details

**Endpoints Updated with Retry Logic**:
- `GetSeasonsAsync()` - Season list fetch
- `GetCharacterTeamsSeasonAsync()` - **Most critical (was failing with HTTP 500)**
- `GetCharacterFullStatsAsync()` - Legacy stats fallback

### 2. Retry Policy with Exponential Backoff

**Configuration**:
- Max retries: 3 attempts per request
- Initial delay: 500ms
- Backoff progression: 500ms → 1s → 2s
- Retry strategy:
  - ✅ Retries on: HTTP 500, 503, timeouts, connection errors
  - ❌ Does NOT retry on: HTTP 4xx client errors (400, 404, etc.)

**Example**: If a request fails with HTTP 500:
1. Wait 500ms, retry
2. If still fails, wait 1s, retry
3. If still fails, wait 2s, retry
4. After 3 attempts, fail with detailed error

### 3. Enhanced Logging in Sc2PulsePlayerStatsService

**File**: `src/engine/Domain/Services/Sc2PulsePlayerStatsService.cs`

**Constructor Setup**:
```csharp
_client.SetDebugLogger(message =>
{
    _logger.Debug("SC2Pulse API: {Message}", message);
});
```

**Added Pre-Request Logging**:
```csharp
_logger.Debug("Attempting to fetch seasonal character-teams data: characterId={CharacterId}, season={SeasonId}, queueType=201", 
    characterId, currentSeason.Id);
```

**Enhanced Error Logging**:
```csharp
catch (HttpRequestException httpEx)
{
    _logger.Warning(httpEx,
        "Seasonal SC2Pulse API call failed for character {CharacterId} ({CharacterName}) in season {SeasonId}. " +
        "Error: {ErrorMessage}. Using legacy stats/full endpoint as fallback.",
        characterId, characterName, currentSeason.Id, httpEx.Message);
}
```

## Log Output Examples

### Successful Request
```
[DEBUG] SC2Pulse API: SC2Pulse API Request: GET /sc2/api/seasons (fetch all seasons)
[DEBUG] SC2Pulse API: SC2Pulse API Response: /sc2/api/seasons 200 125ms
```

### Failed Request with Retry
```
[DEBUG] SC2Pulse API: SC2Pulse API Request: GET /sc2/api/character-teams?characterId=5682900&season=143817&queueType=201 (character-teams for characterId=5682900, season=143817, queueType=201)
[DEBUG] SC2Pulse API: SC2Pulse API Error: /sc2/api/character-teams?characterId=5682900&season=143817&queueType=201 500 - Retrying in 500ms (attempt 1/3)
[DEBUG] SC2Pulse API: SC2Pulse API Response: /sc2/api/character-teams?characterId=5682900&season=143817&queueType=201 500 125ms
[DEBUG] SC2Pulse API: SC2Pulse API Error: /sc2/api/character-teams?characterId=5682900&season=143817&queueType=201 500 - Retrying in 1000ms (attempt 2/3)
[DEBUG] SC2Pulse API: SC2Pulse API Response: /sc2/api/character-teams?characterId=5682900&season=143817&queueType=201 500 118ms
[DEBUG] SC2Pulse API: SC2Pulse API Error: /sc2/api/character-teams?characterId=5682900&season=143817&queueType=201 500 - Not retrying client error
[WARN] Seasonal SC2Pulse API call failed for character 5682900 (Ferz#834) in season 143817. Error: Response status code does not indicate success: 500 (Internal Server Error). Using legacy stats/full endpoint as fallback.
```

### Timeout with Retry
```
[DEBUG] SC2Pulse API: SC2Pulse API Request timeout: /sc2/api/character-teams?... (attempt 1/3) - The operation was canceled.
```

## How to Use for Debugging

### Check Application Logs

Look for lines containing `SC2Pulse API:` in your application logs (Serilog output):

```bash
# On Windows
type debug_output.txt | findstr "SC2Pulse API"

# Or in your log files
Select-String "SC2Pulse API" -Path "*.log"
```

### Understanding the Logs

| Log Pattern | Meaning | Action |
|-----------|---------|--------|
| `API Request: GET ...` | API call starting | Normal operation |
| `API Response: ... 200 Xms` | Successful response | Working correctly |
| `API Response: ... 500 Xms` | Server error | May retry or fallback |
| `API Error: ... Retrying in Yms` | Temporary failure, retrying | Wait for next attempt |
| `API Error: ... Not retrying` | Permanent failure | Will use fallback |

### Interpreting Latency

- **< 100ms**: Fast response (cached or nearby server)
- **100-500ms**: Normal response
- **500-2000ms**: Slow response (may indicate server load)
- **> 2000ms**: Very slow (network issue or server overload)

## What This Fixes

1. **Observability**: You can now see exactly what requests are being made and when they fail
2. **Resilience**: Transient failures (temporary 500 errors, timeouts) are automatically retried
3. **Debugging**: Detailed logs show parameter values, timing, and error messages
4. **Fallback**: When seasonal stats fail, the legacy stats endpoint is used automatically

## What This DOESN'T Fix

- ❌ **HTTP 500 Root Cause**: The underlying reason why the character-teams endpoint returns 500
- ❌ **Invalid Parameters**: If the request parameters are malformed, logging won't fix it
- ❌ **API Deprecation**: If the endpoint is deprecated, retries won't help

## Next Steps for Investigation

With detailed logging now in place, you can:

1. **Collect Logs**: Run the application and capture logs showing the HTTP 500 error
2. **Analyze Parameters**: See exactly what characterId, seasonId, queueType are being sent
3. **Check Timing**: Verify if the endpoint responds consistently or intermittently
4. **Test Alternatives**:
   - Try different season IDs
   - Test with different queueType values
   - Check if the character ID is correct

## Code Files Modified

1. **src/sc2pulse/Sc2PulseClient.cs**
   - Added: `SetDebugLogger()` method
   - Added: `ExecuteWithRetryAsync()` method
   - Updated: `GetSeasonsAsync()`, `GetCharacterTeamsSeasonAsync()`, `GetCharacterFullStatsAsync()`

2. **src/engine/Domain/Services/Sc2PulsePlayerStatsService.cs**
   - Updated: Constructor to set up debug logger
   - Updated: `GetPlayerStatsAsync()` with detailed pre-request and error logging
   - Enhanced: Exception handling with error message context

3. **PLAYERSTATS_DIAGNOSTIC.md**
   - Added: New "Implementation: Logging and Retry Policy" section
   - Documents: All logging strategy and configuration

## Verification

Build status: ✅ **No compilation errors**

All logging is integrated with Serilog and will appear in your configured log outputs (console, file, etc.).
