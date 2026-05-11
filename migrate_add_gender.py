#!/usr/bin/env python3
"""
migrate_add_gender.py
Helper script to add gender information to existing enrolled people.

Usage:
    python migrate_add_gender.py
"""

import json
from pathlib import Path

DATABASE_PATH = Path("models/known_faces.json")

def migrate_database():
    """Add gender field to existing people in database."""
    
    if not DATABASE_PATH.exists():
        print(f"[ERROR] Database not found at {DATABASE_PATH}")
        return
    
    # Load database
    with open(DATABASE_PATH, 'r') as f:
        data = json.load(f)
    
    if "people" not in data:
        print("[ERROR] Invalid database format")
        return
    
    print(f"Found {len(data['people'])} people in database\n")
    
    # Check if any people need gender added
    needs_update = False
    for person in data['people']:
        if "gender" not in person or person["gender"] == "unknown":
            needs_update = True
            break
    
    if not needs_update:
        print("All people already have gender information!")
        return
    
    # Update each person
    updated_count = 0
    for person in data['people']:
        name = person.get("name", "Unknown")
        current_gender = person.get("gender", "unknown")
        
        if current_gender in ["male", "female"]:
            print(f"✓ {name}: {current_gender} (already set)")
            continue
        
        # Ask user for gender
        while True:
            gender = input(f"Enter gender for '{name}' (male/female): ").strip().lower()
            if gender in ["male", "female"]:
                person["gender"] = gender
                updated_count += 1
                print(f"✓ {name}: {gender} (updated)\n")
                break
            else:
                print("  Invalid input. Please enter 'male' or 'female'")
    
    if updated_count > 0:
        # Save updated database
        backup_path = DATABASE_PATH.with_suffix('.backup.json')
        DATABASE_PATH.rename(backup_path)
        print(f"\n[INFO] Created backup at {backup_path}")
        
        with open(DATABASE_PATH, 'w') as f:
            json.dump(data, f, indent=2)
        
        print(f"[INFO] Updated {updated_count} people in database")
        print(f"[INFO] Saved to {DATABASE_PATH}")
    else:
        print("\n[INFO] No updates needed")

if __name__ == "__main__":
    print("=" * 60)
    print("Gender Migration Tool")
    print("=" * 60)
    print()
    migrate_database()
    print()
    print("=" * 60)
    print("Migration complete!")
    print("=" * 60)
