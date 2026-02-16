#!/usr/bin/env python3
"""
Secrets Management Utility for Trade Base

This script helps manage secrets across:
- Local .env files
- GitHub repository secrets
- Encrypted backups

Usage:
    python manage_secrets.py --list
    python manage_secrets.py --sync-to-github
    python manage_secrets.py --check
"""

import argparse
import json
import os
import subprocess
import sys
from pathlib import Path
from typing import Dict, List, Optional, Set

# Add src to path for imports
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'src'))

try:
    from dotenv import load_dotenv
    HAS_DOTENV = True
except ImportError:
    HAS_DOTENV = False


# Secrets that should never be synced to GitHub (local-only)
LOCAL_ONLY_SECRETS = {
    'NINJATRADER_API_SECRET',
    'IB_API_KEY',
    'JWT_SECRET_KEY',
    'ENCRYPTION_KEY',
}

# Required secrets for GitHub Actions
GITHUB_REQUIRED_SECRETS = {
    'DATABASE_URL',
    'DISCORD_WEBHOOK_URL',
}

# All known secrets from .env.example
ALL_KNOWN_SECRETS = {
    'DATABASE_URL', 'DB_HOST', 'DB_PORT', 'DB_NAME', 'DB_USER', 'DB_PASSWORD',
    'NINJATRADER_API_KEY', 'NINJATRADER_API_SECRET', 'NINJATRADER_HOST', 'NINJATRADER_PORT',
    'DISCORD_WEBHOOK_URL', 'DISCORD_WEBHOOK_CRITICAL', 'DISCORD_WEBHOOK_WARNINGS',
    'IB_ACCOUNT_ID', 'IB_API_KEY',
    'ALPACA_API_KEY', 'ALPACA_SECRET_KEY', 'ALPACA_PAPER',
    'MLFLOW_TRACKING_URI', 'MLFLOW_TRACKING_USERNAME', 'MLFLOW_TRACKING_PASSWORD',
    'AWS_ACCESS_KEY_ID', 'AWS_SECRET_ACCESS_KEY', 'AWS_DEFAULT_REGION', 'S3_BUCKET_NAME',
    'MINIO_ACCESS_KEY', 'MINIO_SECRET_KEY', 'MINIO_ENDPOINT',
    'GRAFANA_ADMIN_PASSWORD',
    'PROMETHEUS_REMOTE_WRITE_URL', 'PROMETHEUS_API_KEY',
    'DATADOG_API_KEY', 'DATADOG_APP_KEY',
    'JWT_SECRET_KEY', 'ENCRYPTION_KEY',
    'POLYGON_API_KEY', 'TWELVEDATA_API_KEY', 'CMC_API_KEY',
    'PAPER_TRADING', 'TEST_DATABASE_URL',
    'ENABLE_LIVE_TRADING', 'ENABLE_AUTO_IMPROVEMENT', 'ENABLE_NOTIFICATIONS', 'ENABLE_ML_TRAINING',
}


class SecretsManager:
    """Manages secrets across different storage backends."""
    
    def __init__(self, env_file: str = '.env'):
        self.env_file = Path(env_file)
        self.secrets: Dict[str, str] = {}
        self._load_env()
    
    def _load_env(self):
        """Load secrets from .env file."""
        if HAS_DOTENV and self.env_file.exists():
            load_dotenv(self.env_file)
        
        # Load from environment
        for key in ALL_KNOWN_SECRETS:
            value = os.getenv(key)
            if value:
                self.secrets[key] = value
    
    def list_secrets(self, show_values: bool = False, mask_length: int = 8) -> None:
        """List all known secrets and their status."""
        print("\n" + "=" * 70)
        print("SECRETS INVENTORY")
        print("=" * 70)
        
        categories = {
            'Database': [k for k in ALL_KNOWN_SECRETS if 'DB' in k or 'DATABASE' in k],
            'NinjaTrader': [k for k in ALL_KNOWN_SECRETS if 'NINJATRADER' in k or 'NT_' in k],
            'Discord': [k for k in ALL_KNOWN_SECRETS if 'DISCORD' in k],
            'Brokerage': [k for k in ALL_KNOWN_SECRETS if 'IB_' in k or 'ALPACA' in k],
            'ML/MLOps': [k for k in ALL_KNOWN_SECRETS if 'MLFLOW' in k or 'MINIO' in k or 'S3' in k],
            'Cloud (AWS)': [k for k in ALL_KNOWN_SECRETS if 'AWS' in k],
            'Monitoring': [k for k in ALL_KNOWN_SECRETS if 'GRAFANA' in k or 'PROMETHEUS' in k or 'DATADOG' in k],
            'Security': [k for k in ALL_KNOWN_SECRETS if 'JWT' in k or 'ENCRYPTION' in k],
            'Data Providers': [k for k in ALL_KNOWN_SECRETS if 'API_KEY' in k and not any(x in k for x in ['ALPACA', 'AWS', 'NT_', 'NINJATRADER'])],
            'Feature Flags': [k for k in ALL_KNOWN_SECRETS if k.startswith('ENABLE_') or k == 'PAPER_TRADING'],
        }
        
        for category, keys in categories.items():
            category_secrets = [k for k in keys if k in ALL_KNOWN_SECRETS]
            if not category_secrets:
                continue
            
            print(f"\n📁 {category}")
            print("-" * 70)
            
            for key in sorted(category_secrets):
                if key in self.secrets:
                    value = self.secrets[key]
                    if show_values:
                        # Show full value for non-sensitive or if explicitly requested
                        display_value = value if len(value) < 50 else value[:47] + "..."
                    else:
                        # Mask the value
                        if len(value) <= mask_length * 2:
                            display_value = "*" * len(value)
                        else:
                            display_value = value[:mask_length] + "***" + value[-mask_length:]
                    
                    source = "env"
                    print(f"  ✅ {key:<40} = {display_value:<30} ({source})")
                else:
                    required = "🔴 REQUIRED" if key in GITHUB_REQUIRED_SECRETS else ""
                    print(f"  ❌ {key:<40} {'(not set)':<30} {required}")
        
        # Summary
        total = len(ALL_KNOWN_SECRETS)
        set_count = len(self.secrets)
        print(f"\n{'=' * 70}")
        print(f"Summary: {set_count}/{total} secrets configured")
        print(f"GitHub Required: {len([k for k in GITHUB_REQUIRED_SECRETS if k in self.secrets])}/{len(GITHUB_REQUIRED_SECRETS)}")
    
    def check_secrets(self) -> bool:
        """Check if all required secrets are set."""
        print("\n" + "=" * 70)
        print("REQUIRED SECRETS CHECK")
        print("=" * 70)
        
        missing = []
        
        # Check GitHub required
        print("\n📋 GitHub Actions Required:")
        for key in GITHUB_REQUIRED_SECRETS:
            if key in self.secrets:
                print(f"  ✅ {key}")
            else:
                print(f"  ❌ {key} - MISSING")
                missing.append(key)
        
        # Check local development
        print("\n💻 Local Development Recommended:")
        local_recommended = {'NINJATRADER_API_KEY', 'TEST_DATABASE_URL'}
        for key in local_recommended:
            if key in self.secrets:
                print(f"  ✅ {key}")
            else:
                print(f"  ⚠️  {key} - recommended but optional")
        
        print(f"\n{'=' * 70}")
        if missing:
            print(f"❌ Check failed: {len(missing)} required secrets missing")
            return False
        else:
            print("✅ All required secrets are configured")
            return True
    
    def sync_to_github(self, dry_run: bool = True) -> None:
        """Sync secrets from .env to GitHub repository secrets."""
        print("\n" + "=" * 70)
        print(f"SYNC TO GITHUB {'(DRY RUN)' if dry_run else ''}")
        print("=" * 70)
        
        # Filter out local-only secrets
        github_secrets = {k: v for k, v in self.secrets.items() 
                         if k not in LOCAL_ONLY_SECRETS}
        
        print(f"\nSecrets to sync: {len(github_secrets)}")
        print(f"Skipping (local-only): {len([k for k in self.secrets if k in LOCAL_ONLY_SECRETS])}")
        
        for key, value in sorted(github_secrets.items()):
            if dry_run:
                masked = value[:4] + "***" + value[-4:] if len(value) > 8 else "****"
                print(f"  Would set: {key} = {masked}")
            else:
                try:
                    result = subprocess.run(
                        ['gh', 'secret', 'set', key],
                        input=value,
                        capture_output=True,
                        text=True,
                        check=True
                    )
                    print(f"  ✅ Set: {key}")
                except subprocess.CalledProcessError as e:
                    print(f"  ❌ Failed to set {key}: {e.stderr}")
                except FileNotFoundError:
                    print("  ❌ GitHub CLI (gh) not found. Install from: https://cli.github.com/")
                    return
        
        if dry_run:
            print("\n⚠️  This was a dry run. Use --sync-to-github (without --dry-run) to actually sync.")
    
    def sync_from_github(self, dry_run: bool = True) -> None:
        """Sync secrets from GitHub to local .env file."""
        print("\n" + "=" * 70)
        print(f"SYNC FROM GITHUB {'(DRY RUN)' if dry_run else ''}")
        print("=" * 70)
        
        try:
            result = subprocess.run(
                ['gh', 'secret', 'list', '--json', 'name'],
                capture_output=True,
                text=True,
                check=True
            )
            github_secrets = json.loads(result.stdout)
        except subprocess.CalledProcessError as e:
            print(f"  ❌ Failed to list GitHub secrets: {e.stderr}")
            return
        except FileNotFoundError:
            print("  ❌ GitHub CLI (gh) not found. Install from: https://cli.github.com/")
            return
        
        print(f"\nFound {len(github_secrets)} secrets in GitHub")
        
        env_updates = []
        for secret_meta in github_secrets:
            key = secret_meta['name']
            
            # Skip if already in local env with same value
            if key in self.secrets:
                continue
            
            try:
                result = subprocess.run(
                    ['gh', 'secret', 'get', key],
                    capture_output=True,
                    text=True,
                    check=True
                )
                value = result.stdout.strip()
                env_updates.append((key, value))
                
                if dry_run:
                    print(f"  Would add: {key}")
            except subprocess.CalledProcessError as e:
                print(f"  ⚠️  Could not retrieve {key}: {e.stderr}")
        
        if dry_run:
            print(f"\n⚠️  Would update .env with {len(env_updates)} secrets")
            print("Use --sync-from-github (without --dry-run) to actually sync.")
        else:
            # Append to .env file
            with open(self.env_file, 'a') as f:
                f.write("\n# Synced from GitHub on " + subprocess.run(['date'], capture_output=True, text=True).stdout.strip() + "\n")
                for key, value in env_updates:
                    f.write(f"{key}={value}\n")
            print(f"\n✅ Updated .env with {len(env_updates)} secrets from GitHub")
    
    def generate_env_example(self) -> None:
        """Generate an updated .env.example based on current secrets."""
        example_path = Path('.env.example')
        
        print(f"\n{'=' * 70}")
        print("GENERATE .env.example")
        print("=" * 70)
        
        if example_path.exists():
            print(f"\n⚠️  {example_path} already exists.")
            response = input("Overwrite? (y/N): ")
            if response.lower() != 'y':
                print("Cancelled.")
                return
        
        # Group secrets by category
        categories = {
            'Database': [k for k in ALL_KNOWN_SECRETS if 'DB' in k or 'DATABASE' in k],
            'NinjaTrader API': [k for k in ALL_KNOWN_SECRETS if 'NINJATRADER' in k or 'NT_' in k],
            'Discord Notifications': [k for k in ALL_KNOWN_SECRETS if 'DISCORD' in k],
            # ... etc
        }
        
        print("\nThis would generate a new .env.example file.")
        print("For now, please manually update .env.example with any new secrets.")


def main():
    parser = argparse.ArgumentParser(
        description='Manage secrets for Trade Base project',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s --list                          # List all secrets (masked)
  %(prog)s --list --show-values            # List with visible values
  %(prog)s --check                         # Check required secrets
  %(prog)s --sync-to-github --dry-run      # Preview GitHub sync
  %(prog)s --sync-to-github                # Actually sync to GitHub
  %(prog)s --sync-from-github              # Sync from GitHub to .env
        """
    )
    
    parser.add_argument('--env-file', default='.env',
                       help='Path to .env file (default: .env)')
    parser.add_argument('--list', '-l', action='store_true',
                       help='List all secrets and their status')
    parser.add_argument('--show-values', '-v', action='store_true',
                       help='Show actual secret values (use with caution)')
    parser.add_argument('--check', '-c', action='store_true',
                       help='Check if all required secrets are set')
    parser.add_argument('--sync-to-github', action='store_true',
                       help='Sync secrets from .env to GitHub')
    parser.add_argument('--sync-from-github', action='store_true',
                       help='Sync secrets from GitHub to .env')
    parser.add_argument('--dry-run', '-n', action='store_true',
                       help='Show what would be done without making changes')
    parser.add_argument('--generate-example', action='store_true',
                       help='Generate updated .env.example')
    
    args = parser.parse_args()
    
    # Default action if none specified
    if not any([args.list, args.check, args.sync_to_github, args.sync_from_github, args.generate_example]):
        args.list = True
    
    manager = SecretsManager(env_file=args.env_file)
    
    if args.list:
        manager.list_secrets(show_values=args.show_values)
    
    if args.check:
        success = manager.check_secrets()
        if not success:
            sys.exit(1)
    
    if args.sync_to_github:
        manager.sync_to_github(dry_run=args.dry_run)
    
    if args.sync_from_github:
        manager.sync_from_github(dry_run=args.dry_run)
    
    if args.generate_example:
        manager.generate_env_example()


if __name__ == '__main__':
    main()
