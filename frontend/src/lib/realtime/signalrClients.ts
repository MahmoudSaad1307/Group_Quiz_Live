import * as signalR from "@microsoft/signalr";

export function createHostConnection(): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl("/hubs/host", { withCredentials: true })
    .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
    .build();
}

export function createPlayConnection(screenToken?: string): signalR.HubConnection {
  const url = screenToken
    ? `/hubs/play?role=screen&access_token=${encodeURIComponent(screenToken)}`
    : "/hubs/play";

  return new signalR.HubConnectionBuilder()
    .withUrl(url, { withCredentials: true })
    .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
    .build();
}
