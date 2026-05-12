---
title: "What Do the Green Dots Mean in Microsoft Teams? Complete Guide"
description: "Understand what the green dots, red dots, and yellow dots mean in Microsoft Teams. Complete guide to Teams presence indicators, status colors, and availability states."
pubDate: 2026-05-11T00:00:00.000Z
author: "The Green Dots Team"
keywords: ["teams green dot meaning", "green dot teams", "microsoft teams status colors", "teams presence indicator", "what does green dot mean teams"]
category: "Microsoft Teams Guides"
featured: true
---

## TL;DR - Quick Answer

**Green dots in Microsoft Teams indicate availability status:**

- **Green** = Available (online and ready to chat)
- **Red** = Busy (in a meeting or focused work)
- **Yellow** = Away (stepped away from computer)
- **Gray** = Offline (not signed in to Teams)
- **Purple** = Out of Office (vacation or time off)

---

## Understanding Microsoft Teams Presence Indicators

The colored dot next to someone's profile picture in Microsoft Teams is called a **presence indicator**. It tells you whether someone is available to chat, in a meeting, or offline.

Microsoft Teams uses these presence indicators to help you know the best time to reach out to colleagues without interrupting their work.

## Complete List of Teams Status Colors

### Green Dot - Available

<svg width="24" height="24" viewBox="0 0 24 24" style="display: inline-block; vertical-align: middle;">
  <circle cx="12" cy="12" r="10" fill="#4ade80"/>
</svg>

**Meaning**: The person is online, signed in to Teams, and available to chat.

**What it means for you**: It's safe to send a message or start a call. They'll see your notification immediately.

**When it appears**:
- User is actively using their computer with Teams open
- User has been active in the last 5 minutes

### Red Dot - Busy

<svg width="24" height="24" viewBox="0 0 24 24" style="display: inline-block; vertical-align: middle;">
  <circle cx="12" cy="12" r="10" fill="#f87171"/>
</svg>

**Meaning**: The person is busy and might not respond immediately.

**What it means for you**: They may be in a meeting, on a call, or in focused work mode. Your message won't be blocked, but they might not see it right away.

**When it appears**:
- User is in a Teams meeting or call
- User is sharing their screen
- User manually set status to "Busy" or "Do Not Disturb"
- Calendar shows they're in a meeting

**Busy vs Do Not Disturb**:
- **Busy (red with white circle)**: Notifications still appear but muted
- **Do Not Disturb (red with white line)**: All notifications are suppressed

### Yellow Dot - Away

<svg width="24" height="24" viewBox="0 0 24 24" style="display: inline-block; vertical-align: middle;">
  <circle cx="12" cy="12" r="10" fill="#fbbf24"/>
</svg>

**Meaning**: The person stepped away from their computer.

**What it means for you**: They're still signed in but not actively using Teams. Your message will be delivered, and they'll see it when they return.

**When it appears**:
- User hasn't moved their mouse or typed for 5 minutes
- Computer is locked
- User manually set status to "Be Right Back"

**Auto-Away Timing**: Teams automatically changes status based on inactivity:
- **5 minutes** of inactivity → Status changes to "Away" (yellow)
- **15 minutes** of being away → Status changes to "Offline" (grey)
- **Computer locks** → Immediate change to "Away"

**Note**: Manually setting your status to "Available" doesn't prevent these automatic transitions. If you don't touch your keyboard or mouse, your status will still change to Away after 5 minutes, then Offline after staying away for 15 minutes.

### Gray Dot - Offline

<svg width="24" height="24" viewBox="0 0 24 24" style="display: inline-block; vertical-align: middle;">
  <circle cx="12" cy="12" r="10" fill="#9ca3af"/>
</svg>

**Meaning**: The person is not signed in to Microsoft Teams.

**What it means for you**: Your message will be delivered when they next sign in. They won't see it until then.

**When it appears**:
- User closed Teams completely
- User signed out
- Computer is turned off
- No internet connection
- Automatically after 15 minutes of "Away" status (total 20 minutes of inactivity)

### Purple Dot - Out of Office

<svg width="24" height="24" viewBox="0 0 24 24" style="display: inline-block; vertical-align: middle;">
  <circle cx="12" cy="12" r="10" fill="#a855f7"/>
</svg>

**Meaning**: The person is on vacation or scheduled time off.

**What it means for you**: They're not expected to respond. Often accompanied by an automatic reply message.

**When it appears**:
- User set an Out of Office auto-reply in Outlook
- User manually set status to "Out of Office" in Teams
- Outlook calendar shows extended time off

---

## How Teams Determines Your Status

Microsoft Teams uses **multiple signals** to determine your presence status:

### 1. **Activity Detection**
- Mouse movements
- Keyboard typing
- Touch screen interactions
- Application focus

### 2. **Calendar Integration**
- Outlook calendar meetings
- Teams meeting participation
- Blocked time or focus time

### 3. **Device State**
- Computer lock status
- Screen timeout
- Power saving mode

### 4. **Manual Overrides**
- User manually sets status
- Do Not Disturb schedules
- Out of Office settings

**Priority Order**: Manual status > Calendar meeting > Activity detection > Idle timeout

---

## What Each Status Means for Notifications

| Status | Notifications Shown? | Sound/Popup? | Badge Count? |
|--------|---------------------|--------------|--------------|
| Available (Green) | ✅ Yes | ✅ Yes | ✅ Yes |
| Busy (Red) | ✅ Yes (muted) | ⚠️ Banner only | ✅ Yes |
| Do Not Disturb (Red) | ❌ No | ❌ No | ✅ Yes (silent) |
| Away (Yellow) | ✅ Yes | ✅ Yes | ✅ Yes |
| Offline (Gray) | ❌ Queued | ❌ No | ❌ No |
| Out of Office (Purple) | ⚠️ Auto-reply sent | ❌ No | ⚠️ Depends |

---

## Custom Status Messages

In addition to the colored dot, users can add a **custom status message** like:

- 🏠 Working from home
- 🎧 In deep focus
- ☕ Coffee break
- 🌴 On vacation until Monday

**Custom status appears**:
- Below user's name in chat
- In search results
- On their profile card

**Emojis are popular**: 70% of Teams users add emojis to custom statuses for quick visual context.

---

## Common Questions About Teams Green Dots

### Why is someone's status green but they're not responding?

**Possible reasons**:
1. They have Teams open but are working in other apps
2. They stepped away briefly (within 5-minute window)
3. They're in a meeting but forgot to set status to Busy
4. They have multiple devices with Teams open
5. They're using a "mouse jiggler" to keep status green (not recommended!)

### Can I see someone's Teams status history?

**No, not natively.** Microsoft Teams doesn't save presence history. Once your status changes, the previous data is lost.

**Workaround**: Tools like [The Green Dots](https://thegreendots.io) track and store Teams presence history, showing you 24-hour timelines of when someone was Available, Busy, Away, or Offline.

### How long does "Away" status last?

**Away status is temporary**:
- Automatically set after **5 minutes** of inactivity
- Automatically returns to **Available** when you move your mouse or type
- Manually set "Away" stays until you change it

**Away vs Offline**: Away = Still signed in, just inactive. Offline = Completely signed out.

### Can I fake my Teams status?

**Technically yes, but not recommended:**
- Mouse jiggler hardware keeps status green
- Third-party apps simulate activity
- Custom status can be set manually to "Available"

**Why it's problematic**:
- Violates workplace trust
- Colleagues may try to contact you and get no response
- Some tools violate IT security policies
- Microsoft can detect unusual activity patterns

**Better approach**: Set accurate status or use "Be Right Back" when needed.

### Does Teams show my status to external users?

**It depends on settings:**

**External users in your tenant**: See full status (green/red/yellow/gray)
**Guest users**: See status based on admin settings
**Federated external organizations**: Limited status (usually just Available/Busy)
**Public/anonymous users**: No status shown

Your IT admin controls external status visibility via Teams admin center.

---

## Teams Status Best Practices

### For Remote Workers

✅ **Set "Away" when stepping away**: Even for 10 minutes. Colleagues appreciate knowing you're not ignoring them.

✅ **Use custom status for context**: "🏃 Quick errand, back in 20" is better than generic "Away"

✅ **Schedule Do Not Disturb**: Set automatic DND during lunch or focused work blocks

❌ **Don't fake green status**: Trust is more valuable than appearing online

### For Managers

✅ **Respect status indicators**: Red/purple means they're unavailable. Don't expect immediate responses.

✅ **Track patterns, not minutes**: Use presence data to understand team availability patterns, not micromanage.

✅ **Set expectations clearly**: Clarify when immediate responses are expected vs when async is fine.

### For Distributed Teams

✅ **Check status before calling**: Green = safe to call. Red/yellow = send message first.

✅ **Use presence for scheduling**: Find times when most team members show green status historically.

✅ **Respect timezones**: Someone showing green at 2 AM their time is probably working too much.

---

## Troubleshooting Teams Status Issues

### My status won't change from Away

**Solutions**:
1. Click your profile picture → Set status manually to Available
2. Close and reopen Teams completely
3. Sign out and sign back in
4. Check if "Reset status" setting is enabled (Teams Settings → Privacy)
5. Restart computer if issue persists

### My status shows Available but I'm in a meeting

**Common causes**:
1. Meeting not on your Outlook calendar
2. Third-party meeting tool (Zoom, Google Meet) - Teams doesn't detect these
3. Teams setting "Show me as available when using other apps" is enabled

**Fix**: Manually set status to Busy before meetings, or ensure meetings are on Outlook calendar.

### Teams shows me as Offline when I'm online

**Checklist**:
1. Check internet connection
2. Verify Teams is running (check system tray/menu bar)
3. Sign out and sign back in
4. Clear Teams cache (Help → Clear cache)
5. Check firewall isn't blocking Teams

### Status stuck on "Do Not Disturb"

**How to fix**:
1. Profile picture → Status → Available
2. Check scheduled DND time (Settings → Privacy → Automatic replies)
3. Check Windows Focus Assist (may sync with Teams status)
4. Sign out and back in

---

## How Teams Status Affects Productivity

### Research Findings

**Microsoft Workplace Analytics data** shows:
- Teams with visible presence indicators have **23% fewer** "Are you available?" messages
- Respecting "Busy" status leads to **18% fewer** meeting interruptions
- Using custom status messages reduces **unnecessary ping-backs by 31%**

### Remote Work Benefits

**Presence indicators help remote teams**:
- **Async communication**: Know when to expect responses
- **Meeting scheduling**: Find optimal times across timezones
- **Work-life balance**: Purple status signals time off is respected
- **Interruption management**: Red status protects focused work

---

## What Microsoft Doesn't Tell You

### 1. Status Delays

There's a **1-2 minute delay** between:
- Your actual activity and status change
- Status change and what others see
- Multiple device status sync

**Why**: Reduces server load and prevents rapid flickering between statuses.

### 2. Priority Contacts Override DND

If someone is in your **priority contacts** list, their calls and messages will break through Do Not Disturb status.

**How to set**: Right-click contact → Manage contact → Add to priority access

### 3. Mobile Status is Different

Teams mobile app shows different statuses:
- **Mobile (green with phone icon)**: Using Teams on phone
- Status may not sync instantly with desktop
- Mobile presence updates every 3-5 minutes

### 4. Presence Isn't Always Accurate

**False positives**:
- Browser with Teams tab open = green status
- Multiple signed-in devices = confusion
- Automatic status resets every 3 days

**Status accuracy**: Estimated ~85-90% accurate based on user reports.

---

## Tracking Teams Presence History

**The Problem**: Microsoft Teams doesn't save presence history. You can't see:
- What time someone came online yesterday
- How long they were in meetings
- Patterns of availability over time

**The Solution**: Third-party tracking tools like [The Green Dots](https://thegreendots.io) poll Teams presence every 2 minutes and store historical data.

**Use cases**:
- **Remote team coordination**: Find best meeting times
- **Work-life balance**: Ensure team isn't working excessive hours
- **Timezone optimization**: Identify overlapping availability windows
- **Historical reference**: Review past availability for project planning

[Track Teams presence history with The Green Dots →](https://thegreendots.io/teams-presence-history)

---

## Summary

**Quick Reference Card**:

| Dot Color | Status | Meaning | Response Time |
|-----------|--------|---------|---------------|
| 🟢 Green | Available | Online & ready | Immediate |
| 🔴 Red | Busy/DND | In meeting/focused | Delayed |
| 🟡 Yellow | Away | Stepped away | Within 30 min |
| ⚫ Gray | Offline | Not signed in | Hours |
| 🟣 Purple | Out of Office | Vacation | Days |

**Key Takeaways**:
1. Green dot = safe to message/call
2. Red dot = send message, don't expect immediate response
3. Yellow dot = they'll see it when they return
4. Status auto-updates based on activity, calendar, and device state
5. Custom status messages add helpful context
6. Teams doesn't save presence history (use third-party tools if needed)

**Next steps**:
- Set your Teams status intentionally throughout the day
- Add custom status messages for clarity
- Respect others' status indicators before contacting them
- Consider tracking presence patterns for distributed team coordination

---

**Last updated**: May 11, 2026

**Related articles**:
- [Track Microsoft Teams Presence History](/blog/microsoft-teams-presence-tracking)
- [How to Check Someone's Teams Status History](/blog/check-teams-status-history)
- [Remote Team Management: Best Practices](/blog/remote-team-management-best-practices)
