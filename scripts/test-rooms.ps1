$ErrorActionPreference = "Stop"
$base = "http://localhost:5000/api"
function New-User($name) {
  $body = @{ name = $name; email = "$name@kisstest3.local"; password = "Password123!"; birthDate = "1999-05-10"; gender = "Woman"; acceptTerms = $true } | ConvertTo-Json
  try { $r = Invoke-RestMethod "$base/auth/register" -Method Post -ContentType "application/json" -Body $body }
  catch { $r = Invoke-RestMethod "$base/auth/login" -Method Post -ContentType "application/json" -Body (@{ email = "$name@kisstest3.local"; password = "Password123!" } | ConvertTo-Json) }
  return @{ token = $r.accessToken; id = $r.profile.id; name = $name }
}
function Req($user, $path, $method = "Get", $body = $null) {
  $headers = @{ Authorization = "Bearer $($user.token)" }
  $splat = @{ Uri = "$base$path"; Method = $method; Headers = $headers }
  if ($body) { $splat.ContentType = "application/json"; $splat.Body = ($body | ConvertTo-Json -Depth 5) }
  try { Invoke-RestMethod @splat }
  catch { Write-Host "FAILED $method $path -> $($_.Exception.Message)"; throw }
}
$a = New-User "Alice"
$b = New-User "Bob"
$c = New-User "Carol"
Req $a "/connections/$($b.id)" "Post" | Out-Null
Req $a "/connections/$($c.id)" "Post" | Out-Null
$inboxB = Req $b "/connections"
$inboxC = Req $c "/connections"
Req $b "/connections/$($inboxB[0].id)/accept" "Post" | Out-Null
Req $c "/connections/$($inboxC[0].id)/accept" "Post" | Out-Null
"OK: A-B, A-C are friends"
$room = Req $a "/rooms" "Post" @{ name = "Nhom Thu Nghiem"; memberUserIds = @($b.id, $c.id) }
"OK: room created $($room.id), members=$($room.members.Count)"
Req $a "/rooms/$($room.id)/messages" "Post" @{ content = "Chao ca nhom!" } | Out-Null
$msgs = Req $b "/rooms/$($room.id)/messages"
"OK: B sees $($msgs.Count) message(s), first='$($msgs[0].content)'"
# Group call
$gcall = Req $a "/calls" "Post" @{ callType = "Voice"; roomId = $room.id }
"OK: group call $($gcall.id) participants=$($gcall.participants.Count)"
Req $a "/calls/$($gcall.id)/end" "Post" | Out-Null
# 1-1 call
$call = Req $a "/calls" "Post" @{ callType = "Video"; recipientUserId = $b.id }
"OK: 1-1 call $($call.id) status=$($call.status)"
Req $b "/calls/$($call.id)/answer" "Post" | Out-Null
Req $a "/calls/$($call.id)/end" "Post" | Out-Null
$hist = Req $a "/rooms/calls/history"
"OK: history entries for A: $($hist.Count); last=$($hist[0].status) endedAt=$($hist[0].endedAt)"
# Non-friend cannot be invited
$d = New-User "Dana"
try { Req $a "/rooms/$($room.id)/invite/$($d.id)" "Post" | Out-Null; "PROBLEM: non-friend invite allowed" } catch { "OK: invite of non-friend blocked" }
# Non-member cannot read messages
try { $msgs2 = Req $d "/rooms/$($room.id)/messages" | Out-Null; "PROBLEM: outsider read messages" } catch { "OK: outsider blocked from room messages" }
