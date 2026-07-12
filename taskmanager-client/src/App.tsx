import { useState } from 'react'
import TaskList from './components/TaskList'
import TaskForm from './components/TaskForm'

type Filter = 'all' | 'completed' | 'pending';

function App() {

  const [ refresh, setRefresh ] = useState(0);
  const [ filter, setFilter ] = useState<Filter>('all');
  return (
    <div className="min-h-screen bg-gray-100 p-8 max-w-2xl mx-auto">
      <h1 className="text-3xl font-bold text-blue-600">
        Task Manager
      </h1>
      <TaskForm onTaskCreated={() => setRefresh(r => r + 1)} />

      <div className="flex gap-2 mb-4">
        {(['all', 'completed', 'pending'] as Filter[]).map(f => (
          <button
            key={f}
            className={`px-4 py-1.5 rounded-full text-sm font-medium ${
              filter === f
                ? 'bg-blue-600 text-white'
                : 'bg-gray-300 text-gray-700 hover:bg-gray-50'
            }`}
            onClick={() => setFilter(f)}
          >
            {f === 'all' ? 'Todas' : f === 'completed' ? 'Completadas' : 'Pendientes'}
          </button>
        ))}
      </div>
      <TaskList key={refresh} filter={filter} />
    </div>
  )
}

export default App