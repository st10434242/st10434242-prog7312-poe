import { Link, NavLink, Outlet } from 'react-router-dom'
import { useHubStatus } from '../context/hubContext'
import { useStream } from '../context/streamContext'
import { modules } from '../lib/modules'
import { ConnectionIndicator } from './ConnectionIndicator'
import './AppShell.css'

// Wraps every page so the nav and connection status never unmount when you move between modules
export function AppShell() {
  const status = useHubStatus()
  const { insights } = useStream()

  return (
    <>
      <header className="shell">
        <div className="shell__inner">
          <Link to="/" className="shell__brand">
            Smart-X
          </Link>

          <nav className="shell__nav" aria-label="Modules">
            <NavLink to="/" end className="shell__link">
              Overview
            </NavLink>

            {modules.map((module) =>
              module.route ? (
                <NavLink key={module.name} to={module.route} className="shell__link">
                  {module.shortName}
                  {/* Shows on every page so a new suggestion is not missed while you are on the wall */}
                  {module.route === '/commands' && insights.length > 0 && (
                    <span className="shell__badge" aria-label={`${insights.length} suggested actions`}>
                      {insights.length}
                    </span>
                  )}
                </NavLink>
              ) : (
                <span
                  key={module.name}
                  className="shell__link shell__link--locked"
                  aria-disabled="true"
                  title={`Coming in ${module.lockedUntil}`}
                >
                  {module.shortName}
                  <span className="shell__lock">{module.lockedUntil}</span>
                </span>
              ),
            )}
          </nav>

          <ConnectionIndicator status={status} />
        </div>
      </header>

      <Outlet />
    </>
  )
}
