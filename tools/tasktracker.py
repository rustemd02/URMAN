#!/usr/bin/env python3
"""Read/validate the canonical tracker and render its navigation; no game execution."""
import argparse
import collections
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BACKLOG = ROOT / 'docs/urman_knowledge_base/execution_backlog.json'
INDEX = ROOT / 'docs/tasktracker/TASKS.md'


def load():
    return json.loads(BACKLOG.read_text(encoding='utf-8'))


def nodes(backlog):
    def visit(node, block):
        yield node, block
        for child in node.get('execution', {}).get('units', []):
            yield from visit(child, block)
        for gate in node.get('external_gates', []):
            yield from visit(gate, block)
        if node['id'] == 'STUDIO':
            for step in node['execution']['steps']:
                if step.get('record_type') == 'execution_unit':
                    yield from visit(step, block)
    membership = {tid: block['id'] for block in blocks(backlog) for tid in block['task_ids']}
    for task in backlog['tasks']:
        yield from visit(task, membership[task['id']])


def blocks(backlog):
    return backlog['campaign_delivery_plan']['parallel_blocks']['blocks']


def executable(node):
    return node['task_type'] in ('engineering', 'integration_check', 'research') or (
        node['task_type'] == 'conditional' and node['condition']['state'] == 'true')


def dependency_enabled(dep, registry):
    return not dep.get('when_condition_true') or registry[dep['task_id']].get('condition', {}).get('state') == 'true'


def validate(backlog, before=None):
    errors = []
    def require(condition, message):
        if not condition:
            errors.append(message)
    pairs = list(nodes(backlog))
    ids = [n['id'] for n, _ in pairs]
    require(len(ids) == len(set(ids)), 'Duplicate task/unit/gate ID')
    registry = {n['id']: n for n, _ in pairs}
    members = [tid for block in blocks(backlog) for tid in block['task_ids']]
    task_ids = [t['id'] for t in backlog['tasks']]
    require(collections.Counter(members) == collections.Counter(task_ids), 'Block membership mismatch')
    procedure_ids = [s['id'] for t in backlog['tasks'] for s in t.get('execution', {}).get('steps', [])
                     if s.get('record_type') == 'procedure_step']
    require(len(procedure_ids) == len(set(procedure_ids)) and not set(procedure_ids) & set(ids),
            'Procedure IDs are duplicate or overlap schedulable units')
    schema = backlog['execution_schema']
    for node, _ in pairs:
        tid = node['id']
        for field, allowed in [('task_type', schema['task_types']), ('status', schema['status_values']),
                               ('implementation_state', schema['implementation_values']),
                               ('technical_verification_state', schema['technical_verification_values'])]:
            require(node.get(field) in allowed, f'{tid}: invalid {field}')
        for field in ['assigned_owner', 'active_owner', 'remaining', 'next_action', 'blocker',
                      'external_gates', 'dependencies', 'stop_condition']:
            require(field in node, f'{tid}: missing {field}')
        require(bool(node.get('next_action')), f'{tid}: empty next_action')
        if node['active_owner']:
            require(node.get('activity', {}).get('state') == 'confirmed_active', f'{tid}: unconfirmed active_owner')
        if node['status'] == 'blocked':
            require(bool(node.get('blocker', {}).get('reason')) and bool(node.get('blocker', {}).get('release_when')),
                    f'{tid}: missing blocker/release condition')
        if node['status'] == 'completed' and executable(node):
            require(node['implementation_state'] == 'implemented' and node['technical_verification_state'] == 'passed',
                    f'{tid}: completed without implemented/passed')
        if node['task_type'] == 'conditional':
            require(node.get('condition', {}).get('state') in ['true', 'false', 'not_decided'], f'{tid}: missing condition')
        if executable(node):
            contract = node.get('work_contract', {})
            for field in ['result', 'targets', 'required_inputs', 'scope_in', 'scope_out', 'steps', 'done_when', 'verification']:
                require(bool(contract.get(field)) or field == 'required_inputs', f'{tid}: missing finite contract.{field}')
        for phase in ['implementation', 'verification', 'acceptance']:
            for dep in node.get('dependencies', {}).get(phase, []):
                require(dep['task_id'] in registry, f'{tid}: unresolved {phase} dependency {dep["task_id"]}')
                require(bool(dep.get('artifact')), f'{tid}: unnamed dependency artifact')
        for child in node.get('children', []):
            require(child in registry, f'{tid}: unresolved child {child}')
    for phase in ['implementation', 'verification', 'acceptance']:
        done, visiting = set(), []
        def visit(tid):
            if tid in visiting:
                errors.append(f'{phase} cycle: ' + ' -> '.join(visiting[visiting.index(tid):] + [tid]))
                return
            if tid in done:
                return
            visiting.append(tid)
            for dep in registry[tid].get('dependencies', {}).get(phase, []):
                if dep['task_id'] in registry:
                    visit(dep['task_id'])
            visiting.pop()
            done.add(tid)
        for tid in registry:
            visit(tid)
    # Readiness has three states per node. A gate consumes technical completion;
    # it never waits for the same parent's final acceptance.
    state_graph = {}
    for tid, node in registry.items():
        for phase in ['implementation', 'verification', 'acceptance']:
            key = (tid, phase)
            edges = [] if phase == 'implementation' else [(tid, 'implementation' if phase == 'verification' else 'verification')]
            for dep in node['dependencies'][phase]:
                if not dependency_enabled(dep, registry):
                    continue
                if phase != 'acceptance' and dep.get('availability') == 'available':
                    continue
                target_phase = 'verification' if phase == 'acceptance' and dep.get('requires') in (
                    'technical_closure', 'technical_artifact') else phase
                edges.append((dep['task_id'], target_phase))
            if phase == 'verification' and node['task_type'] == 'aggregate':
                edges.extend((child, 'verification') for child in node.get('children', [])
                             if registry[child]['task_type'] != 'external_acceptance' and
                             (registry[child]['task_type'] != 'conditional' or
                              registry[child]['condition']['state'] == 'true'))
            if phase == 'acceptance':
                edges.extend((gate['id'], 'acceptance') for gate in node.get('external_gates', []))
            state_graph[key] = edges
    done, visiting = set(), []
    def visit_state(key):
        if key in visiting:
            errors.append('Readiness/acceptance cycle: ' + ' -> '.join(
                f'{tid}:{phase}' for tid, phase in visiting[visiting.index(key):] + [key]))
            return
        if key in done:
            return
        visiting.append(key)
        for edge in state_graph.get(key, []):
            visit_state(edge)
        visiting.pop()
        done.add(key)
    for key in state_graph:
        visit_state(key)
    ai = {tid for tid in registry if tid.startswith('STUDIO/AI-') and tid.count('/') == 1}
    require(len(ai) == 45, 'Expected all45 source AI units')
    ai_closure = {d['task_id'] for d in registry['STUDIO/AI-33']['dependencies']['acceptance']}
    require(ai_closure & ai == ai - {'STUDIO/AI-33'}, 'Editor final coverage mismatch')
    legacy = {u['id'] for t in backlog['tasks'] if t['id'].startswith('STUDIO.P')
              for u in t.get('execution', {}).get('units', [])}
    require(set(registry['STUDIO/AI-33'].get('legacy_stage_coverage', [])) == legacy and legacy <= ai_closure,
            'Editor legacy P0–P6 coverage mismatch')
    for tid in ['STUDIO/AI-35', 'ACT1-TAMARA-FENCE', 'REL-004']:
        final_id = 'STUDIO/AI-33' if tid.startswith('STUDIO') else 'REL-003'
        require(any(d['task_id'] == tid and d.get('when_condition_true')
                    for d in registry[final_id]['dependencies']['acceptance']), f'{tid}: conditional final gate missing')
    release = registry['REL-003']['final_coverage']
    mandatory = {t['id'] for t in backlog['tasks'] if t['id'] != 'REL-003' and
                 t['task_type'] != 'conditional' and not t['id'].startswith('STUDIO')}
    require(set(release['mandatory_game_tasks']) == mandatory, 'Full game final coverage mismatch')
    require({d['task_id'] for d in registry['PW-100']['dependencies']['acceptance']} ==
            {f'PW-{n:03}' for n in range(1, 100)}, 'PW100 coverage mismatch')
    if before:
        old = {t['id']: t for t in before['tasks']}
        require(set(old) == set(task_ids), 'Stable parent IDs lost/added')
        for task in backlog['tasks']:
            original = old[task['id']]
            for field in ['owner', 'evidence', 'source_spec', 'source_requirements', 'visual_source_ids',
                          'asset_ids', 'reference_ids', 'integration_2026_10_08']:
                require(task.get(field) == original.get(field), f'{task["id"]}: preserved {field} changed')
            old_steps = {s['id']: s for s in original.get('execution', {}).get('steps', [])}
            new_steps = {s['id']: s for s in task.get('execution', {}).get('steps', [])}
            require(set(old_steps) == set(new_steps), f'{task["id"]}: stable steps lost')
            for sid, step in old_steps.items():
                for field in ['action', 'evidence', 'assigned_owner', 'source_spec', 'source_section', 'required']:
                    require(new_steps[sid].get(field) == step.get(field), f'{sid}: preserved {field} changed')
            require(task.get('history_before_2026_10_11', {}).get('done_when') == original.get('done_when'),
                    f'{task["id"]}: original DoD lost')
        for field in ['requirements_integration_2026_10_08', 'requirements_integration_2026_10_10',
                      'studio_ai_authoring_integration_2026_10_10']:
            require(backlog[field] == before[field], f'{field}: requirement coverage changed')
    if errors:
        raise ValueError('\n'.join(errors))
    return pairs


def render(backlog):
    pairs = list(nodes(backlog))
    counts = collections.Counter(n['task_type'] for n, _ in pairs)
    leaves = sum(executable(n) for n, _ in pairs)
    lines = ['# TASKS.md — навигация единой очереди УРМАН', '',
             f'{len(blocks(backlog))} блоков · {len(backlog["tasks"])} стабильных карточек · {leaves} исполнимых единиц · '
             f'{counts["aggregate"]} агрегатов · {counts["external_acceptance"]} внешних гейтов · {counts["conditional"]} условных единиц.', '',
             'Источник состояний, назначений, результатов и evidence — только [execution_backlog.json](../urman_knowledge_base/execution_backlog.json). '
             'Этот индекс генерирует `python3 tools/tasktracker.py render`; он не хранит статусы. '
             '`python3 tools/tasktracker.py show ID` показывает конечный контракт и следующий шаг; `ready` — доступные входы.', '',
             'Действующий порядок 11.10: [короткое исполнение](06_small_model_execution_2026-09-22.md) и '
             '[полный продукт](07_full_game_integration_2026-10-08.md). Сейчас разрешена только миграция трекера; '
             'живое поручение после миграции указано в execution_schema.current_authority.', '',
             'Процедурные S-шаги не являются самостоятельными задачами. Агрегаты не назначаются разработчикам. '
             'Инженерное завершение и внешняя приёмка считаются отдельно. Условные единицы входят в обязательный остаток только при `condition.state=true`.', '',
             '| Блок | Карточек | Исполнимых единиц | Результат |', '|---|---:|---:|---|']
    for block in blocks(backlog):
        count = sum(executable(n) for n, bid in pairs if bid == block['id'])
        lines.append(f'| [{block["id"]}](#{block["id"].lower()}) | {len(block["task_ids"])} | {count} | {block["visible_result"]} |')
    for block in blocks(backlog):
        lines += ['', f'<a id="{block["id"].lower()}"></a>', f'## {block["id"]} — {block["title"]}', '', block['boundary'], '',
                  '| ID | Тип | Конечный результат / ссылка |', '|---|---|---|']
        for node, bid in pairs:
            if bid == block['id']:
                title = node['title'].replace('|', '\\|').replace('\n', ' ')
                lines.append(f'| `{node["id"]}` | {node["task_type"]} | {title}; [контракт/входы/evidence](../urman_knowledge_base/execution_backlog.json) |')
    return '\n'.join(lines) + '\n'


def ready(node, registry):
    if node['assigned_owner'] == 'unassigned' or not executable(node) or node['status'] in ('completed', 'blocked'):
        return False
    phase = 'verification' if node['task_type'] == 'integration_check' else 'implementation'
    return all(d.get('availability') == 'available' or registry[d['task_id']]['status'] == 'completed'
               for d in node['dependencies'][phase] + (node['dependencies']['implementation'] if phase == 'verification' else [])
               if dependency_enabled(d, registry))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['validate', 'render', 'summary', 'ready', 'show'])
    parser.add_argument('id', nargs='?')
    parser.add_argument('--before', type=Path)
    args = parser.parse_args()
    backlog = load()
    before = json.loads(args.before.read_text()) if args.before else None
    pairs = validate(backlog, before)
    registry = {n['id']: n for n, _ in pairs}
    if args.command == 'render':
        INDEX.write_text(render(backlog), encoding='utf-8')
        print(f'Rendered {INDEX.relative_to(ROOT)}; no statuses duplicated')
    elif args.command == 'validate':
        if INDEX.read_text(encoding='utf-8') != render(backlog):
            raise ValueError('TASKS navigation differs; run render')
        print(f'PASS: {len(backlog["tasks"])} tasks, {len(blocks(backlog))} blocks, {len(pairs)} unique nodes; phase DAGs/coverage/schema/navigation')
    elif args.command == 'show':
        if args.id not in registry:
            parser.error('Unknown ID')
        print(json.dumps(registry[args.id], ensure_ascii=False, indent=2))
    elif args.command == 'ready':
        for node, block in pairs:
            if ready(node, registry):
                print(f'{block} {node["id"]} · {node["assigned_owner"]} · {node["next_action"]}')
    else:
        print('block completed in_progress blocked todo | aggregates external conditional')
        for block in blocks(backlog):
            selected = [n for n, bid in pairs if bid == block['id']]
            statuses = collections.Counter(n['status'] for n in selected if executable(n))
            types = collections.Counter(n['task_type'] for n in selected)
            print(block['id'], *(statuses[s] for s in ['completed', 'in_progress', 'blocked', 'todo']), '|',
                  types['aggregate'], types['external_acceptance'], types['conditional'])


if __name__ == '__main__':
    main()
