# Secrets Management Guide

> **Security Notice:** Never commit secrets to Git. This repository uses `.env` files for local development and GitHub Secrets for CI/CD.

---

## Quick Start

1. **Copy the template:**
   ```bash
   cp .env.example .env
   ```

2. **Fill in your secrets** in `.env`

3. **Load environment variables:**
   ```bash
   source .env
   # Or use a tool like direnv: https://direnv.net/
   ```

---

## Secret Inventory

### Required for Basic Operation

| Secret Name | Purpose | Where Used |
|-------------|---------|------------|
| `DATABASE_URL` | PostgreSQL connection | GitHub Actions, Local scripts |
| `DISCORD_WEBHOOK_URL` | Notifications | GitHub Actions, Local scripts |

### Required for Live Trading

| Secret Name | Purpose | Where Used |
|-------------|---------|------------|
| `NINJATRADER_API_KEY` | NT API authentication | C# Trading Engine |
| `NINJATRADER_API_SECRET` | NT API authentication | C# Trading Engine |

### Optional / Advanced

| Secret Name | Purpose | Where Used |
|-------------|---------|------------|
| `AWS_ACCESS_KEY_ID` | S3 model storage | ML training pipeline |
| `AWS_SECRET_ACCESS_KEY` | S3 model storage | ML training pipeline |
| `MLFLOW_TRACKING_USERNAME` | MLflow auth | ML experiment tracking |
| `MLFLOW_TRACKING_PASSWORD` | MLflow auth | ML experiment tracking |
| `POLYGON_API_KEY` | Alternative market data | Data ingestion |
| `JWT_SECRET_KEY` | API authentication | REST API |

---

## Storage Options

### Option 1: Local `.env` File (Development)

**Best for:** Local development, single developer

```bash
# Create from template
cp .env.example .env

# Edit with your secrets
nano .env

# Source before running scripts
source .env
python scripts/automation/run_improvement_cycle.py
```

**Pros:**
- Simple, no external dependencies
- Works offline

**Cons:**
- File can be accidentally committed
- Hard to sync across multiple machines
- No sharing between team members

---

### Option 2: GitHub Secrets (CI/CD)

**Best for:** GitHub Actions automation

**Setup:**
1. Go to GitHub → Repository → Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Add each secret individually

**Or use the CLI:**
```bash
# Install GitHub CLI if not already: https://cli.github.com/

# Set a secret
cd repos/Trade_base
gh secret set DATABASE_URL < <(echo "postgresql://user:pass@host/db")

# List secrets
gh secret list

# Remove a secret
gh secret delete DATABASE_URL
```

**Secrets needed for GitHub Actions:**
- `DATABASE_URL` - For analytics queries
- `DISCORD_WEBHOOK_URL` - For notifications

---

### Option 3: Password Manager (Recommended for Teams)

**Best for:** Team collaboration, shared secrets

#### Keeper (Recommended for Enterprise)

**Setup:**
```bash
# Install Keeper Commander CLI
pip install keepercommander
# Or: brew install keepercommander

# Login to Keeper
keeper shell

# Create a shared folder for TradeBase
cd /TradeBase
mkdir Secrets

# Add secrets to Keeper
echo "postgresql://..." | keeper add --folder Secrets --title "Database URL" --custom "value"
keeper add --folder Secrets --title "Discord Webhook" --custom "url=https://discord.com/api/webhooks/..."
keeper add --folder Secrets --folder Secrets --title "NinjaTrader API Key" --custom "api_key=..."

# Export all secrets to .env
keeper export --format env --folder Secrets > .env

# Or load specific secrets
export DATABASE_URL=$(keeper get --format text Secrets/"Database URL" value)
export DISCORD_WEBHOOK_URL=$(keeper get --format text Secrets/"Discord Webhook" url)
```

**Automated Loading with Script:**
```bash
# Create a script to load all TradeBase secrets from Keeper
#!/bin/bash
# load_keeper_secrets.sh
FOLDER="Secrets"

echo "Loading TradeBase secrets from Keeper..."
export DATABASE_URL=$(keeper get --format text "$FOLDER/Database URL" value 2>/dev/null)
export DISCORD_WEBHOOK_URL=$(keeper get --format text "$FOLDER/Discord Webhook" url 2>/dev/null)
export NINJATRADER_API_KEY=$(keeper get --format text "$FOLDER/NinjaTrader API Key" api_key 2>/dev/null)
# Add more secrets as needed

echo "Loaded secrets from Keeper folder: $FOLDER"
```

**Sync Keeper to GitHub:**
```bash
# Use our management script with Keeper backend
python scripts/manage_secrets.py --keeper-sync-to-github --dry-run  # Preview
python scripts/manage_secrets.py --keeper-sync-to-github            # Actually sync

# Export from Keeper to local .env
python scripts/manage_secrets.py --keeper-export --keeper-folder Secrets

# Import from local .env to Keeper
python scripts/manage_secrets.py --keeper-import --keeper-folder Secrets
```

**Pros:**
- Enterprise-grade security with zero-knowledge encryption
- Fine-grained access controls and sharing
- Audit logs for compliance
- CLI integration for automation
- Works offline after initial sync

**Cons:**
- Requires Keeper subscription
- Learning curve for CLI usage

---

#### 1Password

**Setup:**
```bash
# Install 1Password CLI: https://developer.1password.com/docs/cli/

# Store secrets in 1Password
echo "postgresql://..." | op item create --category password --title "TradeBase DB" --password=-

# Load secrets from 1Password
eval $(op signin)
export DATABASE_URL=$(op item get "TradeBase DB" --field password)
```

#### Bitwarden

**Setup:**
```bash
# Install Bitwarden CLI: https://bitwarden.com/help/cli/

# Load secrets
bw login
export DATABASE_URL=$(bw get password "TradeBase DB")
```

---

### Option 4: Cloud Secret Managers (Production)

**Best for:** Production deployments, enterprise security

**AWS Secrets Manager:**
```bash
# Store secret
aws secretsmanager create-secret \
  --name tradebase/database_url \
  --secret-string "postgresql://user:pass@host/db"

# Retrieve in application
aws secretsmanager get-secret-value \
  --secret-id tradebase/database_url \
  --query SecretString --output text
```

**Azure Key Vault:**
```bash
# Store secret
az keyvault secret set \
  --vault-name tradebase-vault \
  --name database-url \
  --value "postgresql://user:pass@host/db"
```

**HashiCorp Vault:**
```bash
# Store secret
vault kv put secret/tradebase database_url="postgresql://..."

# Retrieve
vault kv get -field=database_url secret/tradebase
```

---

## Automated Secret Management

### Sync Local .env to GitHub Secrets

Use the provided script:

```bash
# Make script executable
chmod +x scripts/manage_secrets.py

# Preview what would be synced (dry run)
python scripts/manage_secrets.py --dry-run

# Actually sync (requires GitHub authentication)
python scripts/manage_secrets.py --sync-to-github

# Sync from GitHub to local .env
python scripts/manage_secrets.py --sync-from-github
```

### Using the Script

```bash
# Show current secrets (values masked)
python scripts/manage_secrets.py --list

# Check if all required secrets are set
python scripts/manage_secrets.py --check

# Export secrets to encrypted file (for backup)
python scripts/manage_secrets.py --export --output secrets.backup.enc

# Import from encrypted file
python scripts/manage_secrets.py --import secrets.backup.enc
```

---

## Security Best Practices

### ✅ DO

- [ ] Use strong, unique passwords for each service
- [ ] Rotate secrets every 90 days
- [ ] Use different secrets for dev/staging/production
- [ ] Store backup of secrets in encrypted password manager
- [ ] Use `.env.example` as documentation, keep it updated
- [ ] Enable 2FA on all accounts that hold secrets
- [ ] Use service accounts instead of personal credentials

### ❌ DON'T

- [ ] Never commit `.env` files to Git
- [ ] Never share secrets via Slack/email
- [ ] Never hardcode secrets in source code
- [ ] Never use production secrets in development
- [ ] Never log secrets (even partially)

---

## Secret Rotation Schedule

| Secret Type | Rotation Frequency | Responsible |
|-------------|-------------------|-------------|
| Database passwords | Every 90 days | Database Admin |
| API keys (NinjaTrader) | Every 180 days | Trading Ops |
| Discord webhooks | As needed (after leaks) | DevOps |
| Cloud provider keys | Every 90 days | Cloud Admin |
| JWT signing keys | Every 180 days | Security Team |

---

## Emergency Procedures

### If a Secret is Leaked

1. **Immediately revoke/rotate the leaked secret**
   ```bash
   # Example: Rotate Discord webhook
   # 1. Go to Discord Server Settings → Integrations
   # 2. Delete the old webhook
   # 3. Create new webhook
   # 4. Update GitHub Secret and local .env
   ```

2. **Audit access logs** for unauthorized usage

3. **Update all locations:**
   - Local `.env` files
   - GitHub Secrets
   - Password manager
   - Any deployed applications

4. **Notify team** via secure channel

---

## Troubleshooting

### "Secret not found" errors

```bash
# Check if .env is loaded
echo $DATABASE_URL

# If empty, source it
source .env

# Or use python-dotenv
python -c "from dotenv import load_dotenv; load_dotenv(); import os; print(os.getenv('DATABASE_URL'))"
```

### GitHub Actions can't find secrets

1. Verify secret is set: `gh secret list`
2. Check secret name matches exactly (case-sensitive)
3. Ensure workflow has access: Repository → Settings → Actions → General → Workflow permissions

---

## Related Documentation

- [AGENTS.md](AGENTS.md) - Development environment setup
- [docs/improvement-system/README.md](docs/improvement-system/README.md) - CI/CD configuration
- [GitHub Docs: Encrypted Secrets](https://docs.github.com/en/actions/security-guides/encrypted-secrets)
