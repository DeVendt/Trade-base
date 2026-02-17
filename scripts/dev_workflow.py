#!/usr/bin/env python3
"""
Continuous Development Workflow for TradeBase
Runs automated improvement cycles and keeps the Captain informed.

Usage:
    python dev_workflow.py [--interval MINUTES] [--notify]
"""

import argparse
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path

REPO_PATH = Path("/home/cptvendt/repos/Trade_base")
WORKSPACE_PATH = Path("/home/cptvendt/.openclaw/workspace")


def run_command(cmd: str, cwd: Path = REPO_PATH) -> tuple[int, str, str]:
    """Run a shell command and return exit code, stdout, stderr."""
    result = subprocess.run(
        cmd,
        shell=True,
        cwd=cwd,
        capture_output=True,
        text=True
    )
    return result.returncode, result.stdout, result.stderr


def check_git_status():
    """Check for uncommitted changes."""
    code, stdout, _ = run_command("git status --porcelain")
    if stdout.strip():
        return True, stdout.strip()
    return False, ""


def get_recent_commits(n: int = 5) -> str:
    """Get recent commit history."""
    _, stdout, _ = run_command(f"git log --oneline -{n}")
    return stdout.strip()


def commit_changes(message: str) -> bool:
    """Commit all changes with the given message."""
    run_command("git add -A")
    code, _, stderr = run_command(f'git commit -m "{message}"')
    if code != 0:
        print(f"⚠️ Commit failed: {stderr}")
        return False
    return True


def push_changes() -> bool:
    """Push changes to remote."""
    code, _, stderr = run_command("git push origin main")
    if code != 0:
        print(f"⚠️ Push failed: {stderr}")
        return False
    return True


def count_lines_of_code() -> dict:
    """Count lines of code by language."""
    stats = {
        "C#": 0,
        "Python": 0,
        "Markdown": 0,
        "Total": 0
    }
    
    # Count C# files
    code, stdout, _ = run_command("find src -name '*.cs' -exec wc -l {} + | tail -1")
    if code == 0 and stdout:
        try:
            stats["C#"] = int(stdout.split()[0])
        except:
            pass
    
    # Count Python files
    code, stdout, _ = run_command("find scripts -name '*.py' -exec wc -l {} + 2>/dev/null | tail -1 || echo 0")
    if code == 0 and stdout:
        try:
            stats["Python"] = int(stdout.split()[0])
        except:
            pass
    
    stats["Total"] = stats["C#"] + stats["Python"]
    return stats


def generate_progress_report() -> str:
    """Generate a progress report."""
    lines = count_lines_of_code()
    _, commits, _ = run_command("git rev-list --count HEAD")
    
    report = f"""
🏴‍☠️ **TradeBase Development Report** - {datetime.now().strftime('%Y-%m-%d %H:%M UTC')}

**Progress Summary:**
• Total Commits: {commits.strip()}
• C# Code: {lines['C#']:,} lines
• Python Scripts: {lines['Python']:,} lines
• Total: {lines['Total']:,} lines

**Recent Activity:**
```
{get_recent_commits(3)}
```

**Current Status:**
✅ Core infrastructure complete
✅ NinjaTrader adapter with mock mode
✅ Strategy engine implemented
🚧 Risk management system (next)
📝 AI model integration (pending)

Ready for next development cycle!
"""
    return report


def development_cycle(iteration: int):
    """Run one development cycle."""
    print(f"\n{'='*60}")
    print(f"🔄 Development Cycle #{iteration} - {datetime.now().strftime('%H:%M:%S')}")
    print(f"{'='*60}\n")
    
    # Check git status
    has_changes, status = check_git_status()
    
    if has_changes:
        print(f"📁 Uncommitted changes detected:\n{status}\n")
        
        # Auto-commit with timestamp
        commit_msg = f"chore: Development cycle #{iteration} - {datetime.now().strftime('%Y-%m-%d %H:%M')}"
        if commit_changes(commit_msg):
            print(f"✅ Committed: {commit_msg}")
            
            if push_changes():
                print("✅ Pushed to GitHub")
            else:
                print("⚠️ Push failed, will retry next cycle")
    else:
        print("✅ Working tree clean - no changes to commit")
    
    # Generate report
    report = generate_progress_report()
    
    # Save report
    report_file = WORKSPACE_PATH / "dev_reports" / f"report_{datetime.now().strftime('%Y%m%d_%H%M')}.md"
    report_file.parent.mkdir(exist_ok=True)
    report_file.write_text(report)
    
    print(f"\n📊 Progress Report:\n{report}")
    
    return report


def main():
    parser = argparse.ArgumentParser(description="TradeBase Continuous Development Workflow")
    parser.add_argument("--interval", type=int, default=60, help="Minutes between cycles")
    parser.add_argument("--cycles", type=int, default=0, help="Number of cycles (0 = infinite)")
    parser.add_argument("--notify", action="store_true", help="Send Discord notification")
    
    args = parser.parse_args()
    
    print("🏴‍☠️ TradeBase Continuous Development Workflow")
    print(f"Repository: {REPO_PATH}")
    print(f"Interval: {args.interval} minutes")
    print(f"{'='*60}\n")
    
    iteration = 1
    
    try:
        while args.cycles == 0 or iteration <= args.cycles:
            report = development_cycle(iteration)
            
            # If notification requested, could send to Discord here
            if args.notify:
                print("📡 Would send notification to Discord (implement me!)")
            
            iteration += 1
            
            if args.cycles == 0 or iteration <= args.cycles:
                print(f"\n⏳ Sleeping for {args.interval} minutes...")
                time.sleep(args.interval * 60)
    
    except KeyboardInterrupt:
        print("\n\n⚓ Development workflow stopped by user")
        print("Fair winds, Captain!")
        return 0
    
    return 0


if __name__ == "__main__":
    sys.exit(main())
