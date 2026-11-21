# QuizLive Free Deployment Guide

This guide covers two **$0/month** ways to deploy QuizLive in production.

---

## Option A: Oracle Cloud "Always Free" ARM VM (Recommended)

Oracle Cloud Infrastructure (OCI) gives an **Always Free** Ampere A1 VM (up to 4 ARM OCPUs, 24 GB RAM, 200 GB storage) with **no sleep/cold-start**, full WebSocket support, and single-origin cookies.

### 1. Create the VM & Open Ports
1. Sign up at [cloud.oracle.com](https://cloud.oracle.com/) and create a Compute Instance:
   - **Image:** Ubuntu 24.04 (Canonical)
   - **Shape:** `VM.Standard.A1.Flex` (Ampere ARM — choose 2 OCPUs / 12 GB RAM or up to 4 OCPUs / 24 GB RAM; all within Always Free)
2. In your VCN **Security List** (Ingress Rules), allow TCP ports **80** and **443** from `0.0.0.0/0`.
3. SSH into the VM and open the OS firewall + install Docker & Caddy:
   ```bash
   sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 80 -j ACCEPT
   sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 443 -j ACCEPT
   sudo netfilter-persistent save

   # Install Docker + Compose plugin
   curl -fsSL https://get.docker.com | sudo sh
   sudo usermod -aG docker $USER

   # Install Caddy (automatic HTTPS + WebSocket reverse proxy)
   sudo apt update && sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https curl
   curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
   curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
   sudo apt update && sudo apt install -y caddy
   ```
   Log out and back in so `docker` works without `sudo`.

### 2. Clone, Configure `.env`, and Start
```bash
git clone https://github.com/MahmoudSaad1307/Group_Quiz_Live.git quizlive
cd quizlive
cp .env.example .env
```

Edit `.env` with strong production values:
```env
ASPNETCORE_ENVIRONMENT=Production
POSTGRES_USER=quizlive
POSTGRES_PASSWORD=<generate-strong-32-char-password>
POSTGRES_DB=quizlive

Jwt__Issuer=QuizLive
Jwt__Audience=QuizLive
Jwt__SigningKey=<generate-strong-64-char-random-secret>
Jwt__ExpiryMinutes=480

SeedAdmin__Email=admin@yourdomain.com
SeedAdmin__Password=<strong-admin-password>
SeedAdmin__DisplayName=QuizLive Admin

Cors__AllowedOrigins__0=https://quiz.yourdomain.com
NEXT_PUBLIC_APP_URL=https://quiz.yourdomain.com
API_INTERNAL_URL=http://api:8080
```

Start the stack:
```bash
docker compose up -d --build
```

### 3. Enable HTTPS with Caddy
Point your domain's `A` record (or a free DuckDNS/nip.io subdomain) to the VM's public IP, then edit `/etc/caddy/Caddyfile`:
```caddyfile
quiz.yourdomain.com {
    reverse_proxy localhost:3000
}
```
Reload Caddy:
```bash
sudo systemctl reload caddy
```
Visit `https://quiz.yourdomain.com/admin/login` and sign in!

---

## Option B: Free PaaS Split (Neon + Koyeb/Render + Vercel)

If you don't want to manage a VM:
1. **PostgreSQL (Free):** Create a database on [Neon](https://neon.tech) or [Supabase](https://supabase.com) and copy the Postgres connection string.
2. **Backend API (Free):** Deploy `backend/src/QuizLive.Api/Dockerfile` as a Docker Web Service on [Koyeb](https://www.koyeb.com) or [Render](https://render.com) with:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `Db__AutoMigrate=true` (for initial setup)
   - `ConnectionStrings__Default=<your-neon-connection-string>`
   - `Jwt__SigningKey=<64-char-secret>`
   - `SeedAdmin__Email`, `SeedAdmin__Password`, `SeedAdmin__DisplayName`
   - `Cors__AllowedOrigins__0=https://your-app.vercel.app`
3. **Frontend (Free):** Import the repo into [Vercel](https://vercel.com) with **Root Directory** set to `frontend`, and set:
   - `API_INTERNAL_URL=https://your-api-url.koyeb.app`
   - `NEXT_PUBLIC_APP_URL=https://your-app.vercel.app`

> **Important caveat for Option B:** Vercel serverless rewrites do not proxy long-lived WebSockets (`/hubs/*`), so SignalR falls back to Long Polling unless you set `NEXT_PUBLIC_API_URL` directly in `src/lib/realtime/signalrClients.ts` or use Option A. Render's free tier also sleeps after 15 minutes of inactivity (~30–50s cold start).
