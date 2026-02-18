# Anotador IA - Setup na VPS

> Este guia deve ser executado na VPS onde o backend sera hospedado.
> Pode ser executado por uma sessao do Claude Code diretamente na VM.

## Pre-requisitos

- Linux (Ubuntu 22.04+ recomendado)
- .NET 10 SDK
- Pelo menos 8GB RAM (Whisper large + Ollama)
- GPU opcional (melhora performance do Whisper e Ollama)

---

## Passo 1: Instalar .NET 10

```bash
# Ubuntu/Debian
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0

# Adicionar ao PATH
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools' >> ~/.bashrc
source ~/.bashrc

# Verificar
dotnet --version
```

## Passo 2: Instalar Ollama

```bash
curl -fsSL https://ollama.ai/install.sh | sh

# Verificar que esta rodando
ollama --version
systemctl status ollama

# Baixar o modelo (llama3.1 8B - ~4.7GB)
ollama pull llama3.1

# Testar
ollama run llama3.1 "Diga 'ola' em portugues" --verbose
```

**Modelos alternativos:**
- `llama3.1` (8B) - Recomendado para VPS com 8-16GB RAM
- `llama3.1:70b` - Para VPS com 64GB+ RAM e GPU
- `mistral` (7B) - Alternativa leve
- `gemma2` (9B) - Boa qualidade para o tamanho

## Passo 3: Baixar modelo Whisper

```bash
# Criar diretorio de modelos dentro do projeto
cd /caminho/para/anotador-api/AnotadorApi.Web
mkdir -p models

# Baixar modelo Whisper large-v3 (recomendado para qualidade)
wget -O models/ggml-large-v3.bin \
  https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin

# Alternativas menores (se a VPS tiver pouca RAM):
# Medium (~1.5GB): https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin
# Small (~466MB):  https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin
# Base (~142MB):   https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin
```

**Tamanhos dos modelos:**
| Modelo | Tamanho | RAM necessaria | Qualidade |
|--------|---------|---------------|-----------|
| base | 142MB | ~1GB | Basica |
| small | 466MB | ~2GB | Boa |
| medium | 1.5GB | ~5GB | Muito boa |
| large-v3 | 3.1GB | ~10GB | Excelente |

## Passo 4: Configurar o backend

```bash
cd /caminho/para/anotador-api/AnotadorApi.Web

# Criar appsettings.Production.json com as credenciais reais
cat > appsettings.Production.json << 'EOF'
{
  "App": {
    "SupabaseUrl": "https://fbftlodruzrzzfylqdik.supabase.co",
    "SupabaseServiceKey": "SUA_SERVICE_ROLE_KEY_AQUI",
    "WhisperModelPath": "models/ggml-large-v3.bin",
    "OllamaUrl": "http://localhost:11434",
    "OllamaModel": "llama3.1"
  },
  "Urls": "http://0.0.0.0:5000",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
EOF
```

**Para obter a Service Role Key do Supabase:**
1. Acesse https://supabase.com/dashboard/project/fbftlodruzrzzfylqdik/settings/api
2. Copie a `service_role` key (nao a anon key!)
3. Substitua `SUA_SERVICE_ROLE_KEY_AQUI` no arquivo acima

## Passo 5: Build e executar

```bash
cd /caminho/para/anotador-api

# Restaurar dependencias
dotnet restore

# Build em modo release
dotnet build -c Release

# Executar
ASPNETCORE_ENVIRONMENT=Production dotnet run -c Release --project AnotadorApi.Web
```

O servidor vai iniciar em `http://0.0.0.0:5000`.

**Testar:**
```bash
curl http://localhost:5000/health
# Esperado: {"status":"healthy","timestamp":"..."}
```

## Passo 6: Configurar como servico (systemd)

```bash
sudo cat > /etc/systemd/system/anotador-api.service << EOF
[Unit]
Description=Anotador IA API
After=network.target ollama.service

[Service]
Type=simple
User=$USER
WorkingDirectory=/caminho/para/anotador-api/AnotadorApi.Web
ExecStart=/usr/bin/dotnet run -c Release
Environment=ASPNETCORE_ENVIRONMENT=Production
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable anotador-api
sudo systemctl start anotador-api

# Verificar status
sudo systemctl status anotador-api
sudo journalctl -u anotador-api -f
```

## Passo 7: Configurar reverse proxy (Nginx)

```bash
sudo apt install nginx -y

sudo cat > /etc/nginx/sites-available/anotador-api << 'EOF'
server {
    listen 80;
    server_name SEU_DOMINIO_OU_IP;

    client_max_body_size 500M;  # Permitir upload de audios grandes

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 300s;  # Timeout maior para processamento
    }
}
EOF

sudo ln -s /etc/nginx/sites-available/anotador-api /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx
```

## Passo 8: SSL com Certbot (opcional, recomendado)

```bash
sudo apt install certbot python3-certbot-nginx -y
sudo certbot --nginx -d SEU_DOMINIO
```

---

## Checklist final

- [ ] .NET 10 instalado (`dotnet --version`)
- [ ] Ollama rodando (`ollama --version`, `systemctl status ollama`)
- [ ] Modelo LLM baixado (`ollama list` mostra llama3.1)
- [ ] Modelo Whisper baixado (arquivo `models/ggml-large-v3.bin` existe)
- [ ] `appsettings.Production.json` com Service Role Key do Supabase
- [ ] Backend rodando (`curl http://localhost:5000/health`)
- [ ] Servico systemd habilitado
- [ ] Nginx configurado com proxy reverso
- [ ] SSL (opcional)

## Troubleshooting

**Ollama nao responde:**
```bash
systemctl restart ollama
journalctl -u ollama -f
```

**Whisper falha ao carregar modelo:**
- Verificar se o arquivo nao esta corrompido: `sha256sum models/ggml-large-v3.bin`
- Tentar um modelo menor se nao tiver RAM suficiente

**Backend nao conecta no Supabase:**
- Verificar se a Service Role Key esta correta
- Testar: `curl -H "apikey: SUA_KEY" https://fbftlodruzrzzfylqdik.supabase.co/rest/v1/meetings`

**Out of memory:**
- Usar modelo Whisper menor (small ou medium)
- Usar modelo Ollama menor (mistral 7B)
- Aumentar swap: `sudo fallocate -l 8G /swapfile && sudo chmod 600 /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile`
